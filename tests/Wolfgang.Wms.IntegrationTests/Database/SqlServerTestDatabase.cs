// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// A throw-away SQL Server database for one test (E13.1): a fresh database in a container when Docker is
/// available, or on the instance <see cref="EnvironmentVariable"/> names (SQL Server Express LocalDB on the
/// Windows job, a developer's local Express) when it is set — the variable wins, so the Windows install
/// path is exercised where containers are not. Either way the test gets a database of its own, never
/// <c>master</c>: the schema, the history table and the runtime login's grants land where an install puts them. Dropped on dispose either way, with the runtime login
/// <see cref="TestLogins"/> may have created for it (server-scoped, so it would outlive the database).
/// </summary>
[ExcludeFromCodeCoverage]   // the instance path runs only where WMS_TEST_SQLSERVER is set, the container path only where Docker is; no one machine covers both
public sealed class SqlServerTestDatabase : IAsyncDisposable
{
    /// <summary>
    /// A connection string to a SQL Server instance the tests may create databases on.
    /// </summary>
    public const string EnvironmentVariable = "WMS_TEST_SQLSERVER";

    /// <summary>
    /// The container image used when Docker serves the test.
    /// </summary>
    public const string DefaultImage = "mcr.microsoft.com/mssql/server:2022-latest";



    private readonly IContainer? _container;
    private readonly string? _instance;
    private readonly string? _name;



    private SqlServerTestDatabase(string connectionString, IContainer? container, string? instance, string? name)
    {
        ConnectionString = connectionString;
        _container = container;
        _instance = instance;
        _name = name;
    }



    /// <summary>
    /// The connection string of the test's database.
    /// </summary>
    public string ConnectionString { get; }



    /// <summary>
    /// True while the database is available.
    /// </summary>
    public bool Running => _container is null || _container.State == TestcontainersStates.Running;



    /// <summary>
    /// True when <see cref="EnvironmentVariable"/> names an instance.
    /// </summary>
    public static bool InstanceIsConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable));



    /// <summary>
    /// Starts the database: a new one on the configured instance, else a new one in a container of
    /// <paramref name="image"/> (whose connection string points at <c>master</c>, which no test should use).
    /// Nothing started is left behind on failure: a container whose database could not be created is disposed
    /// before the exception leaves (CI runs without the Ryuk reaper, #843, so nothing else would).
    /// <paramref name="name"/> overrides the generated database name; tests use it to force a failure.
    /// </summary>
    public static async Task<SqlServerTestDatabase> StartAsync(string image = DefaultImage, CancellationToken cancellationToken = default, string? name = null)
    {
        name ??= "wms_test_" + Guid.NewGuid().ToString("N")[..12];
        var instance = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(instance))
        {
            await CreateAsync(instance, name, cancellationToken);
            return new SqlServerTestDatabase(new SqlConnectionStringBuilder(instance) { InitialCatalog = name }.ConnectionString, container: null, instance, name);
        }

        var container = new MsSqlBuilder(image).Build();
        await container.StartAsync(cancellationToken);
        try
        {
            await CreateAsync(container.GetConnectionString(), name, cancellationToken);
        }
        catch
        {
            await container.DisposeAsync();   // no owner yet; without this the engine keeps running
            throw;
        }

        return new SqlServerTestDatabase(new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = name }.ConnectionString, container, instance: null, name);
    }



    private static async Task CreateAsync(string server, string name, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(server);
        await connection.OpenAsync(cancellationToken);
        await using var create = connection.CreateCommand();
        create.CommandText = "CREATE DATABASE [" + name + "]";
        await create.ExecuteNonQueryAsync(cancellationToken);
    }



    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        await using var connection = new SqlConnection(_instance);
        await connection.OpenAsync();
        await using var drop = connection.CreateCommand();
        drop.CommandText = string.Format(CultureInfo.InvariantCulture, "IF DB_ID('{0}') IS NOT NULL BEGIN ALTER DATABASE [{0}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{0}]; END", _name);
        await drop.ExecuteNonQueryAsync();

        // the runtime login's sessions died with the database; the login itself is server-scoped
        var login = TestLogins.RuntimeLoginName(_name!);
        await using var dropLogin = connection.CreateCommand();
        dropLogin.CommandText = string.Format(CultureInfo.InvariantCulture, "IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'{0}') BEGIN DECLARE @kill nvarchar(max) = N''; SELECT @kill += N'KILL ' + CAST(session_id AS nvarchar(10)) + N';' FROM sys.dm_exec_sessions WHERE login_name = N'{0}'; EXEC(@kill); DROP LOGIN [{0}]; END", login);
        await dropLogin.ExecuteNonQueryAsync();
    }
}
