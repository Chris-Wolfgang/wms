// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// A throw-away SQL Server database for one test (E13.1): a container when Docker is available, or a fresh
/// database on the instance <see cref="EnvironmentVariable"/> names (SQL Server Express LocalDB on the
/// Windows job, a developer's local Express) when it is set — the variable wins, so the Windows install
/// path is exercised where containers are not. Dropped on dispose either way.
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
    /// True when the database is served by a container (else by the named instance).
    /// </summary>
    public bool IsContainer => _container is not null;



    /// <summary>
    /// True while the database is available.
    /// </summary>
    public bool Running => _container is null || _container.State == TestcontainersStates.Running;



    /// <summary>
    /// True when <see cref="EnvironmentVariable"/> names an instance.
    /// </summary>
    public static bool InstanceIsConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable));



    /// <summary>
    /// Starts the database: a new one on the configured instance, else a container of <paramref name="image"/>.
    /// </summary>
    public static async Task<SqlServerTestDatabase> StartAsync(string image = DefaultImage, CancellationToken cancellationToken = default)
    {
        var instance = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(instance))
        {
            var name = "wms_test_" + Guid.NewGuid().ToString("N")[..12];
            await using (var connection = new SqlConnection(instance))
            {
                await connection.OpenAsync(cancellationToken);
                await using var create = connection.CreateCommand();
                create.CommandText = "CREATE DATABASE [" + name + "]";
                await create.ExecuteNonQueryAsync(cancellationToken);
            }

            var builder = new SqlConnectionStringBuilder(instance) { InitialCatalog = name };
            return new SqlServerTestDatabase(builder.ConnectionString, container: null, instance, name);
        }

        var container = new MsSqlBuilder(image).Build();
        await container.StartAsync(cancellationToken);
        return new SqlServerTestDatabase(container.GetConnectionString(), container, instance: null, name: null);
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
    }
}
