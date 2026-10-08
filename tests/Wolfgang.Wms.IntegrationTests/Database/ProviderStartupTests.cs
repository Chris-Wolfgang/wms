// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text.Json;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E2.2 / E2.3 / E4.4 / E4.6 against real engines in containers: on each supported provider the API refuses to
/// start on an unmigrated database (naming the pending migration), starts with <c>AutoMigrate</c> and then
/// reports the schema up to date, and refuses to start, even with <c>AutoMigrate</c>, once a newer build has
/// migrated the database (naming the unknown migration). The SQL Server 2025 container runs as Express (<c>MSSQL_PID</c>), the free edition
/// customers start on.
/// </summary>
public sealed class ProviderStartupTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;



    public ProviderStartupTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [SqlServerFact]
    public async Task SqlServer_2022_starts_and_migrates()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();

        Assert.True(database.Running);
        await AssertStartsAndMigratesAsync(container: null, "SqlServer", database.ConnectionString, trustServerCertificate: true);
    }



    [DockerFact]
    public async Task SqlServer_2025_Express_starts_and_migrates()
    {
        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
            .WithEnvironment("MSSQL_PID", "Express")
            .Build();
        await container.StartAsync();

        await AssertStartsAndMigratesAsync(container, "SqlServer", container.GetConnectionString(), trustServerCertificate: true);
    }



    [DockerFact]
    public async Task PostgreSql_16_starts_and_migrates()
    {
        await using var container = new PostgreSqlBuilder("postgres:16")
            .Build();
        await container.StartAsync();

        await AssertStartsAndMigratesAsync(container, "PostgreSql", container.GetConnectionString(), trustServerCertificate: false);
    }



    private async Task AssertStartsAndMigratesAsync(IContainer? container, string provider, string connectionString, bool trustServerCertificate)
    {
        if (container is not null)
        {
            Assert.Equal(TestcontainersStates.Running, container.State);
        }

        using var unmigrated = Host(provider, connectionString, trustServerCertificate, autoMigrate: false);
        var refused = Assert.Throws<InvalidOperationException>(() => unmigrated.CreateClient());
        Assert.Contains("pending migrations", refused.Message, StringComparison.Ordinal);
        Assert.Contains("_Initial", refused.Message, StringComparison.Ordinal);

        using var host = Host(provider, connectionString, trustServerCertificate, autoMigrate: true);
        using var client = host.CreateClient();
        var status = await SchemaAsync(client);

        Assert.EndsWith("_Initial", status.RootElement.GetProperty("expected").GetString(), StringComparison.Ordinal);
        Assert.Equal(status.RootElement.GetProperty("expected").GetString(), status.RootElement.GetProperty("current").GetString());
        Assert.True(status.RootElement.GetProperty("upToDate").GetBoolean());

        using var migrated = Host(provider, connectionString, trustServerCertificate, autoMigrate: false);
        using var migratedClient = migrated.CreateClient();
        Assert.True((await SchemaAsync(migratedClient)).RootElement.GetProperty("upToDate").GetBoolean());

        await AssertRefusesASchemaAheadOfTheBuildAsync(provider, connectionString, trustServerCertificate);
    }



    private async Task AssertRefusesASchemaAheadOfTheBuildAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        await FutureMigration.RecordAsync(provider, connectionString);

        foreach (var autoMigrate in new[] { false, true })
        {
            using var ahead = Host(provider, connectionString, trustServerCertificate, autoMigrate);
            var refused = Assert.Throws<InvalidOperationException>(() => ahead.CreateClient());

            Assert.Contains("The database schema is newer than this build", refused.Message, StringComparison.Ordinal);
            Assert.Contains(FutureMigration.Id, refused.Message, StringComparison.Ordinal);
            Assert.Contains("Upgrade the application, or restore the backup taken before the upgrade.", refused.Message, StringComparison.Ordinal);
        }
    }



    private WebApplicationFactory<Program> Host(string provider, string connectionString, bool trustServerCertificate, bool autoMigrate)
    {
        return _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Wms:Database:Provider", provider)
            .UseSetting("Wms:Database:ConnectionString", connectionString)
            .UseSetting("Wms:Database:TrustServerCertificate", trustServerCertificate ? "true" : "false")
            .UseSetting("Wms:Database:AutoMigrate", autoMigrate ? "true" : "false"));
    }



    private static async Task<JsonDocument> SchemaAsync(HttpClient client)
    {
        using var response = await client.GetAsync(new Uri("/api/v0/system/schema", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
