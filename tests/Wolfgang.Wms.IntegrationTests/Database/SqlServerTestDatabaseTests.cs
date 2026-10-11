// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Docker.DotNet.Models;
using DotNet.Testcontainers.Configurations;
using Microsoft.Data.SqlClient;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// The throw-away database factory itself (E13.1): every test gets a database of its own, never <c>master</c>,
/// and what the factory starts, it also cleans up when it fails.
/// </summary>
public sealed class SqlServerTestDatabaseTests
{
    /// <summary>
    /// SQL Server's error for "Incorrect syntax near ...", which the unbracketable name below provokes; the number
    /// is stable across the server's languages, the message is not.
    /// </summary>
    private const int IncorrectSyntax = 102;



    [SqlServerFact]
    public async Task StartAsync_gives_the_test_a_database_of_its_own_and_connects_to_it()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();
        var catalog = new SqlConnectionStringBuilder(database.ConnectionString).InitialCatalog;
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_NAME()";
        var current = (string)(await command.ExecuteScalarAsync())!;

        Assert.StartsWith("wms_test_", catalog, StringComparison.Ordinal);   // the regression of #898: the container path handed out master
        Assert.Equal(catalog, current);
    }



    [SqlServerFact]
    public async Task StartAsync_leaves_nothing_running_when_the_database_cannot_be_created()
    {
        // An unbracketable name makes CREATE DATABASE fail after the engine is up.
        var exception = await Assert.ThrowsAsync<SqlException>(() => SqlServerTestDatabase.StartAsync(name: "x]y"));

        Assert.Equal(IncorrectSyntax, exception.Number);
    }



    [DockerFact]
    public async Task StartInContainerAsync_disposes_the_container_when_the_database_cannot_be_created()
    {
        // On the container path the failure comes at the moment a started container has no owner yet; the factory
        // must dispose it before rethrowing. Docker is asked, by the run's label, whether the container is gone.
        var runId = Guid.NewGuid().ToString("N");

        var exception = await Assert.ThrowsAsync<SqlException>(() => SqlServerTestDatabase.StartInContainerAsync(SqlServerTestDatabase.DefaultImage, "x]y", runId, CancellationToken.None));
        using var docker = TestcontainersSettings.OS.DockerEndpointAuthConfig.GetDockerClientBuilder(Guid.NewGuid()).Build();   // the daemon Testcontainers used
        var leftBehind = await docker.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>(StringComparer.Ordinal)
            {
                ["label"] = new Dictionary<string, bool>(StringComparer.Ordinal) { [SqlServerTestDatabase.RunLabel + "=" + runId] = true },
            },
        });

        Assert.Equal(IncorrectSyntax, exception.Number);
        Assert.Empty(leftBehind);
    }
}
