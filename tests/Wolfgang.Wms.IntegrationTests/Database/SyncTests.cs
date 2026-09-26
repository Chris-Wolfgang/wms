// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Database.Conventions;
using Wolfgang.Wms.Infrastructure.Database.Sync;
using Wolfgang.Wms.IntegrationTests.Database.TestModels;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E5.3/E5.4 against real engines through the generated endpoints: deltas page in <c>row_version</c> order
/// with a monotonic watermark, an update and a soft delete surface in the next delta, the manifest lists
/// live rows only, and the default query hides soft-deleted rows.
/// </summary>
public sealed class SyncTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;



    [DockerFact]
    public async Task SqlServer_deltas_and_manifest()
    {
        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync();

        await AssertSyncAsync(new DatabaseOptions { Provider = "SqlServer", ConnectionString = container.GetConnectionString(), TrustServerCertificate = true });
    }



    [DockerFact]
    public async Task PostgreSql_deltas_and_manifest()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertSyncAsync(new DatabaseOptions { Provider = "PostgreSql", ConnectionString = container.GetConnectionString() });
    }



    [Fact]
    public void MapSyncedTable_when_an_argument_is_null_or_blank_throws()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        using var app = builder.Build();
        Func<SyncSampleDbContext, IQueryable<SyncThing>> source = c => c.Things;
        Func<SyncThing, long> project = t => t.Id;

        Assert.Throws<ArgumentNullException>(() => SyncEndpoints.MapSyncedTable(null!, "/things", source, project));
        Assert.Throws<ArgumentException>(() => app.MapSyncedTable(" ", source, project));
        Assert.Throws<ArgumentNullException>(() => app.MapSyncedTable<SyncSampleDbContext, SyncThing, long>("/things", null!, project));
        Assert.Throws<ArgumentNullException>(() => app.MapSyncedTable<SyncSampleDbContext, SyncThing, long>("/things", source, null!));
    }



    private static async Task AssertSyncAsync(DatabaseOptions options)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<SyncSampleDbContext>(o => DatabaseServiceCollectionExtensions.Configure(o, options));
        await using var app = builder.Build();
        app.MapGroup("/api/v0").MapSyncedTable<SyncSampleDbContext, SyncThing, ThingDto>("/things", c => c.Things, t => new ThingDto(t.Id, t.Name, t.RowVersion, t.DeletedAt));
        await app.StartAsync();
        using var client = app.GetTestClient();

        var seeded = await SeedAsync(app.Services);
        var watermark = await AssertPagesAsync(client, seeded);
        await AssertChangesAppearAsync(client, app.Services, watermark);
    }



    private static async Task<List<SyncThing>> SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SyncSampleDbContext>();
        await context.Database.EnsureCreatedAsync();
        await context.Database.ExecuteSqlRawAsync(RowVersioning.UpdateTriggerSql(context.Database.ProviderName!, "picking", "sync_thing"));
        var things = Enumerable.Range(1, 7).Select(i => new SyncThing { Name = "thing " + i }).ToList();
        context.Things.AddRange(things);
        await context.SaveChangesAsync();
        Assert.True(things.Select(t => t.RowVersion).Zip(things.Skip(1).Select(t => t.RowVersion)).All(p => p.Second > p.First), "insert defaults must be monotonic");
        return things;
    }



    private static async Task<long> AssertPagesAsync(HttpClient client, List<SyncThing> seeded)
    {
        var page1 = (await client.GetFromJsonAsync<DeltaDto>("/api/v0/things?since=0&size=3", Json))!;
        var page2 = (await client.GetFromJsonAsync<DeltaDto>($"/api/v0/things?since={page1.NextSince}&size=3", Json))!;
        var page3 = (await client.GetFromJsonAsync<DeltaDto>($"/api/v0/things?since={page2.NextSince}&size=3", Json))!;
        var single = (await client.GetFromJsonAsync<DeltaDto>("/api/v0/things?size=0", Json))!;

        Assert.Equal([1, 2, 3], page1.Items.Select(i => i.Id));
        Assert.True(page1.HasMore);
        Assert.Equal(seeded[2].RowVersion, page1.NextSince);
        Assert.Equal([4, 5, 6], page2.Items.Select(i => i.Id));
        Assert.True(page2.HasMore);
        Assert.Equal([7], page3.Items.Select(i => i.Id));
        Assert.False(page3.HasMore);
        Assert.Equal(page2.NextSince, page3.NextSince);   // final page steps back by the margin but never below what was sent
        Assert.Single(single.Items);   // size is clamped to 1..MaxPageSize; since defaults to 0
        return page3.NextSince;
    }



    private static async Task AssertChangesAppearAsync(HttpClient client, IServiceProvider services, long watermark)
    {
        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SyncSampleDbContext>();
            (await context.Things.SingleAsync(t => t.Id == 2)).Name = "thing 2 renamed";
            (await context.Things.SingleAsync(t => t.Id == 5)).DeletedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
        }

        var delta = (await client.GetFromJsonAsync<DeltaDto>($"/api/v0/things?since={watermark}", Json))!;
        var manifest = (await client.GetFromJsonAsync<List<ManifestEntryDto>>("/api/v0/things/manifest", Json))!;

        Assert.Equal([7, 2, 5], delta.Items.Select(i => i.Id));   // row_version order: untouched 7, then the two writes
        Assert.Equal("thing 2 renamed", delta.Items[1].Name);
        Assert.NotNull(delta.Items[2].DeletedAt);
        Assert.False(delta.HasMore);
        Assert.True(delta.NextSince >= watermark);
        Assert.Equal([1, 2, 3, 4, 6, 7], manifest.Select(m => m.Id));
        Assert.Equal(delta.Items[1].RowVersion, manifest[1].RowVersion);
        using var check = services.CreateScope();
        var live = check.ServiceProvider.GetRequiredService<SyncSampleDbContext>();
        Assert.Equal(6, await live.Things.CountAsync());
        Assert.Equal(7, await live.Things.IgnoreQueryFilters().CountAsync());
    }
}
