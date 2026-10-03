// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text.Json;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E2.2 / E2.3 against real engines in containers: the API starts on each supported provider, reports the
/// schema as not yet migrated, applies both providers' migrations, and then reports the schema up to date.
/// The SQL Server 2025 container runs as Express (<c>MSSQL_PID</c>), the free edition customers start on.
/// </summary>
public sealed class ProviderStartupTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;



    public ProviderStartupTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [DockerFact]
    public async Task SqlServer_2022_starts_and_migrates()
    {
        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
        await container.StartAsync();

        await AssertStartsAndMigratesAsync(container, "SqlServer", container.GetConnectionString(), trustServerCertificate: true);
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



    private async Task AssertStartsAndMigratesAsync(IContainer container, string provider, string connectionString, bool trustServerCertificate)
    {
        Assert.Equal(TestcontainersStates.Running, container.State);
        using var host = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Wms:Database:Provider", provider)
            .UseSetting("Wms:Database:ConnectionString", connectionString)
            .UseSetting("Wms:Database:TrustServerCertificate", trustServerCertificate ? "true" : "false"));
        using var client = host.CreateClient();

        var before = await SchemaAsync(client);
        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<WmsDbContext>().Database.MigrateAsync();
        }

        var after = await SchemaAsync(client);

        Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("current").ValueKind);
        Assert.EndsWith("_Initial", before.RootElement.GetProperty("expected").GetString(), StringComparison.Ordinal);
        Assert.False(before.RootElement.GetProperty("upToDate").GetBoolean());
        Assert.Equal(after.RootElement.GetProperty("expected").GetString(), after.RootElement.GetProperty("current").GetString());
        Assert.True(after.RootElement.GetProperty("upToDate").GetBoolean());
    }



    private static async Task<JsonDocument> SchemaAsync(HttpClient client)
    {
        using var response = await client.GetAsync(new Uri("/api/v0/system/schema", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
