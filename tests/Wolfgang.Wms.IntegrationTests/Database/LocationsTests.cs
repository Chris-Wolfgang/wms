// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wolfgang.AuditTrail.Entities;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Http.Paging;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Core.Zones;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.IntegrationTests.Api;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E17.1 against real engines through the API: an unknown site is 404; an empty page on a fresh site; seven bins
/// across two zones; a duplicate code in another case and a duplicate barcode are 409, a zone of another site
/// 400, a walk sequence outside the zone's prefix 400, a malformed draft 400; keyset pages forward through the
/// walk order with stable cursors, backward from a later page, by descending code, by barcode, filtered by zone
/// and by id window, with the exact total and the id bounds; a cursor under another sort is refused; read with
/// ETag; update needs <c>If-Match</c> and refuses a code or barcode another bin holds; every write is audited.
/// </summary>
public sealed class LocationsTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;



    [SqlServerFact]
    public async Task SqlServer_creates_pages_and_updates_locations()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();

        await AssertLocationsAsync("SqlServer", database.ConnectionString, trustServerCertificate: true);
    }



    [DockerFact]
    public async Task PostgreSql_creates_pages_and_updates_locations()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertLocationsAsync("PostgreSql", container.GetConnectionString(), trustServerCertificate: false);
    }



    private static async Task AssertLocationsAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        await using var app = await StartHostAsync(provider, connectionString, trustServerCertificate);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", await TestSessions.SignInAsAdministratorAsync(client));
        var site = await CreateSiteAsync(client, "DC1");
        var other = await CreateSiteAsync(client, "DC2");
        var zoneA = await CreateZoneAsync(client, site.Id, "A", "A");
        var zoneB = await CreateZoneAsync(client, site.Id, "B", null);
        var zoneX = await CreateZoneAsync(client, other.Id, "X", null);

        using var unknownSite = await client.GetAsync(new Uri($"/api/v0/sites/{site.Id + other.Id + 100}/locations", UriKind.Relative));
        var empty = await client.GetFromJsonAsync<Page<LocationInfo>>($"/api/v0/sites/{site.Id}/locations", Json);
        var created = new List<LocationInfo>();
        for (var i = 1; i <= 5; i++)
        {
            created.Add(await CreateLocationAsync(client, site.Id, new LocationDraft($"A-01-0{i}", $"L{i}", zoneA.Id, $"A-010{i}")));
        }

        created.Add(await CreateLocationAsync(client, site.Id, new LocationDraft("B-01-01", "L6", zoneB.Id, "B-0101")));
        created.Add(await CreateLocationAsync(client, site.Id, new LocationDraft("B-01-02", "L7", zoneB.Id, "B-0102", IsPickable: false)));
        using var duplicateCode = await client.PostAsync(Locations(site.Id), Body(new LocationDraft("a-01-01", "L99", zoneA.Id, "A-0199")));
        using var duplicateBarcode = await client.PostAsync(Locations(site.Id), Body(new LocationDraft("A-01-99", "L1", zoneA.Id, "A-0199")));
        using var foreignZone = await client.PostAsync(Locations(site.Id), Body(new LocationDraft("X-01-01", "L98", zoneX.Id, "X-0101")));
        using var outsidePrefix = await client.PostAsync(Locations(site.Id), Body(new LocationDraft("A-01-98", "L97", zoneA.Id, "C-0198")));
        using var malformed = await client.PostAsync(Locations(site.Id), Body(new LocationDraft("A-01-97", "L 96", zoneA.Id, "A-0197")));
        using var read = await client.GetAsync(new Uri($"/api/v0/sites/{site.Id}/locations/{created[0].Id}", UriKind.Relative));
        using var wrongSite = await client.GetAsync(new Uri($"/api/v0/sites/{other.Id}/locations/{created[0].Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, unknownSite.StatusCode);
        Assert.Equal("locations.site_not_found", await CodeAsync(unknownSite));
        Assert.Equal((0L, 0, (string?)null, (string?)null, (long?)null, (long?)null), (empty!.TotalCount, empty.Items.Count, empty.NextCursor, empty.PreviousCursor, empty.MinId, empty.MaxId));
        Assert.Equal((site.Id, zoneA.Id, "A-01-01", "L1", "A-0101", true, true, "admin"), (created[0].SiteId, created[0].ZoneId, created[0].Code, created[0].Barcode, created[0].WalkSequence, created[0].IsPickable, created[0].IsActive, created[0].UpdatedBy));
        Assert.False(created[6].IsPickable);
        Assert.Equal(HttpStatusCode.Conflict, duplicateCode.StatusCode);
        Assert.Equal("locations.code_taken", await CodeAsync(duplicateCode));
        Assert.Equal(HttpStatusCode.Conflict, duplicateBarcode.StatusCode);
        Assert.Equal("locations.barcode_taken", await CodeAsync(duplicateBarcode));
        Assert.Equal(HttpStatusCode.BadRequest, foreignZone.StatusCode);
        Assert.Equal("locations.zone_not_found", await CodeAsync(foreignZone));
        Assert.Equal(HttpStatusCode.BadRequest, outsidePrefix.StatusCode);
        Assert.Contains("walk-order prefix 'A'", await DetailAsync(outsidePrefix), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("locations.invalid", await CodeAsync(malformed));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(created[0].Etag, read.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.NotFound, wrongSite.StatusCode);
        Assert.Equal("locations.not_found", await CodeAsync(wrongSite));

        await AssertPagingAsync(client, site.Id, zoneB.Id, created);
        await AssertUpdateAsync(client, site.Id, zoneB.Id, created);
        await AssertAuditAsync(app.Services);
    }



    private static async Task AssertPagingAsync(HttpClient client, long siteId, long zoneBId, List<LocationInfo> created)
    {
        var ids = created.Select(l => l.Id).ToList();

        var first = await PageAsync(client, siteId, "size=3");
        var second = await PageAsync(client, siteId, $"size=3&after={first.NextCursor}");
        var third = await PageAsync(client, siteId, $"size=3&after={second.NextCursor}");
        var back = await PageAsync(client, siteId, $"size=3&before={third.PreviousCursor}");
        var byCodeDescending = await PageAsync(client, siteId, "size=2&sort=-code");
        var byBarcode = await PageAsync(client, siteId, "size=10&sort=barcode");
        var byIdDescending = await PageAsync(client, siteId, "size=2&sort=-id");
        var afterId = await PageAsync(client, siteId, $"size=10&sort=-id&after={byIdDescending.NextCursor}");
        var zoneB = await PageAsync(client, siteId, $"zone_id={zoneBId}");
        var window = await PageAsync(client, siteId, $"id_from={ids[1]}&id_to={ids[3]}");
        using var wrongSort = await client.GetAsync(new Uri($"/api/v0/sites/{siteId}/locations?sort=code&after={first.NextCursor}", UriKind.Relative));

        Assert.Equal((7L, ids[0], ids[6]), (first.TotalCount, first.MinId, first.MaxId));
        Assert.Equal(["A-0101", "A-0102", "A-0103"], first.Items.Select(l => l.WalkSequence));
        Assert.Null(first.PreviousCursor);
        Assert.Equal(["A-0104", "A-0105", "B-0101"], second.Items.Select(l => l.WalkSequence));
        Assert.NotNull(second.PreviousCursor);
        Assert.Equal(["B-0102"], third.Items.Select(l => l.WalkSequence));
        Assert.Null(third.NextCursor);
        Assert.NotNull(third.PreviousCursor);
        Assert.Equal(second.Items.Select(l => l.Id), back.Items.Select(l => l.Id));
        Assert.NotNull(back.PreviousCursor);
        Assert.NotNull(back.NextCursor);
        Assert.Equal(["B-01-02", "B-01-01"], byCodeDescending.Items.Select(l => l.Code));
        Assert.Equal(["L1", "L2", "L3", "L4", "L5", "L6", "L7"], byBarcode.Items.Select(l => l.Barcode));
        Assert.Null(byBarcode.NextCursor);
        Assert.Equal([ids[6], ids[5]], byIdDescending.Items.Select(l => l.Id));
        Assert.Equal([ids[4], ids[3], ids[2], ids[1], ids[0]], afterId.Items.Select(l => l.Id));
        Assert.Equal((2L, 2), (zoneB.TotalCount, zoneB.Items.Count));
        Assert.All(zoneB.Items, l => Assert.Equal(zoneBId, l.ZoneId));
        Assert.Equal((3L, ids[1], ids[3]), (window.TotalCount, window.MinId, window.MaxId));
        Assert.Equal(HttpStatusCode.BadRequest, wrongSort.StatusCode);
        Assert.Contains("was issued for sort 'walk_sequence'", await DetailAsync(wrongSort), StringComparison.Ordinal);
    }



    private static async Task AssertUpdateAsync(HttpClient client, long siteId, long zoneBId, List<LocationInfo> created)
    {
        var target = created[0];
        var moved = new LocationDraft(target.Code, target.Barcode, zoneBId, "B-0000", IsPickable: false);

        using var noPrecondition = await client.PutAsync(Location(siteId, target.Id), Body(moved));
        using var updated = await client.SendAsync(Put(siteId, target.Id, moved, target.Etag));
        var updatedInfo = (await updated.Content.ReadFromJsonAsync<LocationInfo>(Json))!;
        using var stale = await client.SendAsync(Put(siteId, target.Id, moved, target.Etag));
        using var codeClash = await client.SendAsync(Put(siteId, target.Id, moved with { Code = "b-01-01" }, updatedInfo.Etag));
        using var barcodeClash = await client.SendAsync(Put(siteId, target.Id, moved with { Barcode = "L7" }, updatedInfo.Etag));
        using var unknown = await client.SendAsync(Put(siteId, created[^1].Id + 1000, moved, "\"1\""));

        Assert.Equal(HttpStatusCode.PreconditionRequired, noPrecondition.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal((zoneBId, "B-0000", false), (updatedInfo.ZoneId, updatedInfo.WalkSequence, updatedInfo.IsPickable));
        Assert.True(updatedInfo.RowVersion > target.RowVersion);
        Assert.Equal(updatedInfo.Etag, updated.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, codeClash.StatusCode);
        Assert.Equal("locations.code_taken", await CodeAsync(codeClash));
        Assert.Equal(HttpStatusCode.Conflict, barcodeClash.StatusCode);
        Assert.Equal("locations.barcode_taken", await CodeAsync(barcodeClash));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }



    private static async Task AssertAuditAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var headers = await context.Set<AuditHeader>().Include(h => h.Details).Where(h => h.EntityTable.Contains("location")).ToListAsync();
        var rows = await context.Locations.ToListAsync();

        Assert.True(headers.Count >= 8, $"expected seven creates and one update to be audited; found {headers.Count}");
        Assert.Contains(headers.SelectMany(h => h.Details), d => string.Equals(d.ColumnName, "walk_sequence", StringComparison.Ordinal) && string.Equals(d.ValueText, "B-0000", StringComparison.Ordinal));
        Assert.Equal(7, rows.Count);
        Assert.All(rows, r => Assert.Equal("admin", r.UpdatedBy));
    }



    private static async Task<Page<LocationInfo>> PageAsync(HttpClient client, long siteId, string query)
    {
        using var response = await client.GetAsync(new Uri($"/api/v0/sites/{siteId}/locations?{query}", UriKind.Relative));
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{query}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<Page<LocationInfo>>(Json))!;
    }



    private static async Task<LocationInfo> CreateLocationAsync(HttpClient client, long siteId, LocationDraft draft)
    {
        using var response = await client.PostAsync(Locations(siteId), Body(draft));
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{draft.Code}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var info = (await response.Content.ReadFromJsonAsync<LocationInfo>(Json))!;
        Assert.Equal(info.Etag, response.Headers.ETag!.ToString());
        return info;
    }



    private static async Task<SiteInfo> CreateSiteAsync(HttpClient client, string code)
    {
        using var response = await client.PostAsync(new Uri("/api/v0/sites", UriKind.Relative), new StringContent(JsonSerializer.Serialize(new SiteDraft(code, code, "UTC"), Json), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SiteInfo>(Json))!;
    }



    private static async Task<ZoneInfo> CreateZoneAsync(HttpClient client, long siteId, string code, string? walkOrderPrefix)
    {
        var draft = new ZoneDraft(code, "Zone " + code, ZoneType.Pick, walkOrderPrefix, IsRejectLane: false, Resolution: null);
        using var response = await client.PostAsync(new Uri($"/api/v0/sites/{siteId}/zones", UriKind.Relative), new StringContent(JsonSerializer.Serialize(draft, Json), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ZoneInfo>(Json))!;
    }



    private static Uri Locations(long siteId)
    {
        return new Uri($"/api/v0/sites/{siteId}/locations", UriKind.Relative);
    }



    private static Uri Location(long siteId, long locationId)
    {
        return new Uri($"/api/v0/sites/{siteId}/locations/{locationId}", UriKind.Relative);
    }



    private static HttpRequestMessage Put(long siteId, long locationId, LocationDraft draft, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, Location(siteId, locationId)) { Content = Body(draft) };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return request;
    }



    private static StringContent Body(LocationDraft draft)
    {
        return new StringContent(JsonSerializer.Serialize(draft, Json), Encoding.UTF8, "application/json");
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }



    private static async Task<string> DetailAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("detail").GetString() ?? string.Empty;
    }



    private static async Task<WebApplication> StartHostAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Wms:Database:Provider"] = provider,
            ["Wms:Database:ConnectionString"] = connectionString,
            ["Wms:Database:TrustServerCertificate"] = trustServerCertificate ? "true" : "false",
            ["Wms:Database:AutoMigrate"] = "true",
        });
        builder.Services.AddWmsApiVersioning();
        builder.Services.AddWmsProblemDetails();
        builder.Services.AddWmsModules();
        builder.Services.AddWmsSettingsModule();
        builder.Services.AddWmsAuthModule();
        builder.Services.AddWmsSitesModule();
        builder.Services.AddWmsZonesModule();
        builder.Services.AddWmsLocationsModule();
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        var app = builder.Build();
        app.UseWmsProblemDetails();
        app.UseWmsAuth();
        app.MapWmsApi().MapWmsModules();
        await app.StartAsync();
        return app;
    }
}
