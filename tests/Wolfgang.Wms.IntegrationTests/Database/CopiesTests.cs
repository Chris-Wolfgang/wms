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
using Wolfgang.Wms.Core.Copies;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Core.Zones;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.IntegrationTests.Api;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E16.5 against real engines through the API: a site copy brings the settings overrides, the zones (a
/// resolution zone with its resolvers included) and the locations, every copy carrying <c>copiedFromId</c>;
/// a second copy with a prefix substitution renumbers codes and barcodes; a copy without zones brings nothing
/// beneath; a taken site code is 409, an unknown site 404, a half substitution 400; a zone copy within the
/// site needs a substitution and then renumbers its locations, a zone copy across sites brings its settings;
/// a location copy needs free code and barcode and an existing zone; a range copy renumbers codes, barcodes
/// and walk sequences, is all or nothing, and reports no match as 404; the audit records the copied-from id.
/// </summary>
public sealed class CopiesTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private static readonly SettingKey<TimeSpan> LeaseTimeout = new("sample.lease_timeout", TimeSpan.FromMinutes(15), "How long a picker holds a task.");



    [SqlServerFact]
    public async Task SqlServer_copies_sites_zones_and_locations()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();

        await AssertCopiesAsync("SqlServer", database.ConnectionString, trustServerCertificate: true);
    }



    [DockerFact]
    public async Task PostgreSql_copies_sites_zones_and_locations()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertCopiesAsync("PostgreSql", container.GetConnectionString(), trustServerCertificate: false);
    }



    private static async Task AssertCopiesAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        await TestMigrations.ApplyAsync(provider, connectionString);
        var runtime = await TestLogins.CreateRuntimeAsync(provider, connectionString);

        await using var app = await StartHostAsync(provider, runtime, trustServerCertificate);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", await TestSessions.SignInAsAdministratorAsync(client));
        var source = await PostAsync<SiteInfo>(client, "/api/v0/sites", new SiteDraft("DC1", "First", "Europe/Berlin"));
        var aisle = await PostAsync<ZoneInfo>(client, $"/api/v0/sites/{source.Id}/zones", new ZoneDraft("A01", "Aisle 1", ZoneType.Pick, "A", IsRejectLane: true, Resolution: null));
        var adminId = await AdminIdAsync(app.Services);
        var resolution = await PostAsync<ZoneInfo>(client, $"/api/v0/sites/{source.Id}/zones", new ZoneDraft("RES", "Resolution", ZoneType.Resolution, null, IsRejectLane: false, new ResolutionZone("RESTOCK", null, [adminId], true, false, true, false, true)));
        var bin1 = await PostAsync<LocationInfo>(client, $"/api/v0/sites/{source.Id}/locations", new LocationDraft("A-01-01", "A-01-01-BC", aisle.Id, "A-0101"));
        var bin2 = await PostAsync<LocationInfo>(client, $"/api/v0/sites/{source.Id}/locations", new LocationDraft("A-01-02", "A-01-02-BC", aisle.Id, "A-0102", IsPickable: false));
        await SetAsync(app.Services, SettingScopeRef.Site(source.Id), TimeSpan.FromMinutes(40));
        await SetAsync(app.Services, SettingScopeRef.Zone(aisle.Id), TimeSpan.FromMinutes(35));

        var copy = await AssertSiteCopyAsync(client, app.Services, source, aisle, resolution, bin1, bin2, adminId);
        await AssertZoneCopiesAsync(client, app.Services, source, copy, aisle);
        await AssertLocationCopiesAsync(client, source, aisle, bin1);
        await AssertAuditAsync(app.Services);
    }



    private static async Task<SiteInfo> AssertSiteCopyAsync(HttpClient client, IServiceProvider services, SiteInfo source, ZoneInfo aisle, ZoneInfo resolution, LocationInfo bin1, LocationInfo bin2, long adminId)
    {
        var copy = await PostAsync<SiteInfo>(client, $"/api/v0/sites/{source.Id}/copy", new SiteCopyRequest("DC2", "Second"));
        var zones = (await client.GetFromJsonAsync<List<ZoneInfo>>($"/api/v0/sites/{copy.Id}/zones", Json))!;
        var locations = (await client.GetFromJsonAsync<Core.Http.Paging.Page<LocationInfo>>($"/api/v0/sites/{copy.Id}/locations", Json))!.Items;
        var copiedAisle = zones.Single(z => string.Equals(z.Code, "A01", StringComparison.Ordinal));
        var copiedResolution = zones.Single(z => string.Equals(z.Code, "RES", StringComparison.Ordinal));
        var siteSetting = await ValueAsync(services, SettingScopeRef.Site(copy.Id));
        var zoneSetting = await ValueAsync(services, SettingScopeRef.Zone(copiedAisle.Id));
        var renumbered = await PostAsync<SiteInfo>(client, $"/api/v0/sites/{source.Id}/copy", new SiteCopyRequest("DC3", "Third", "UTC", Settings: false, CodePrefixFrom: "A-", CodePrefixTo: "B-"));
        var renumberedLocations = (await client.GetFromJsonAsync<Core.Http.Paging.Page<LocationInfo>>($"/api/v0/sites/{renumbered.Id}/locations", Json))!.Items;
        var bare = await PostAsync<SiteInfo>(client, $"/api/v0/sites/{source.Id}/copy", new SiteCopyRequest("DC4", "Fourth", Zones: false));
        var bareZones = await client.GetFromJsonAsync<List<ZoneInfo>>($"/api/v0/sites/{bare.Id}/zones", Json);
        using var taken = await client.PostAsync(new Uri($"/api/v0/sites/{source.Id}/copy", UriKind.Relative), Body(new SiteCopyRequest("dc2", "Again")));
        using var unknown = await client.PostAsync(new Uri($"/api/v0/sites/{source.Id + 1000}/copy", UriKind.Relative), Body(new SiteCopyRequest("DC9", "Nine")));
        using var half = await client.PostAsync(new Uri($"/api/v0/sites/{source.Id}/copy", UriKind.Relative), Body(new SiteCopyRequest("DC9", "Nine", CodePrefixFrom: "A-")));

        Assert.Equal((source.Id, "DC2", "Second", "Europe/Berlin"), (copy.CopiedFromId, copy.Code, copy.Name, copy.TimeZone));
        Assert.Equal(["A01", "RES"], zones.Select(z => z.Code));
        Assert.Equal((aisle.Id, "A", true), (copiedAisle.CopiedFromId, copiedAisle.WalkOrderPrefix, copiedAisle.IsRejectLane));
        Assert.Equal((resolution.Id, "RESTOCK", true), (copiedResolution.CopiedFromId, copiedResolution.Resolution!.RestockingBin, copiedResolution.Resolution.IsVirtualQueue));
        Assert.Equal([adminId], copiedResolution.Resolution.ResolverUserIds);
        Assert.Equal(["A-01-01", "A-01-02"], locations.Select(l => l.Code));
        Assert.Equal([bin1.Id, bin2.Id], locations.Select(l => l.CopiedFromId));
        Assert.All(locations, l => Assert.Equal(copiedAisle.Id, l.ZoneId));
        Assert.False(locations[1].IsPickable);
        Assert.Equal(("00:40:00", "00:35:00"), (siteSetting, zoneSetting));
        Assert.Equal("UTC", renumbered.TimeZone);
        Assert.Equal(["B-01-01", "B-01-02"], renumberedLocations.Select(l => l.Code));
        Assert.Equal(["B-01-01-BC", "B-01-02-BC"], renumberedLocations.Select(l => l.Barcode));
        Assert.Equal("00:15:00", await ValueAsync(services, SettingScopeRef.Site(renumbered.Id)));   // settings not brought: the default
        Assert.Empty(bareZones!);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("copies.code_taken", await CodeAsync(taken));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("copies.not_found", await CodeAsync(unknown));
        Assert.Equal(HttpStatusCode.BadRequest, half.StatusCode);
        Assert.Equal("copies.invalid", await CodeAsync(half));
        return copy;
    }



    private static async Task AssertZoneCopiesAsync(HttpClient client, IServiceProvider services, SiteInfo source, SiteInfo target, ZoneInfo aisle)
    {
        using var needsSubstitution = await client.PostAsync(new Uri($"/api/v0/sites/{source.Id}/zones/{aisle.Id}/copy", UriKind.Relative), Body(new ZoneCopyRequest("A02", "Aisle 2")));
        var within = await PostAsync<ZoneInfo>(client, $"/api/v0/sites/{source.Id}/zones/{aisle.Id}/copy", new ZoneCopyRequest("A02", "Aisle 2", CodePrefixFrom: "A-01", CodePrefixTo: "A-02"));
        var withinLocations = (await client.GetFromJsonAsync<Core.Http.Paging.Page<LocationInfo>>($"/api/v0/sites/{source.Id}/locations?zone_id={within.Id}", Json))!.Items;
        var across = await PostAsync<ZoneInfo>(client, $"/api/v0/sites/{source.Id}/zones/{aisle.Id}/copy", new ZoneCopyRequest("A09", "Aisle 9", target.Id, Locations: false));
        using var takenCode = await client.PostAsync(new Uri($"/api/v0/sites/{source.Id}/zones/{aisle.Id}/copy", UriKind.Relative), Body(new ZoneCopyRequest("a02", "Again", Locations: false)));
        using var unknownTarget = await client.PostAsync(new Uri($"/api/v0/sites/{source.Id}/zones/{aisle.Id}/copy", UriKind.Relative), Body(new ZoneCopyRequest("A03", "Three", target.Id + 1000, Locations: false)));

        Assert.Equal(HttpStatusCode.BadRequest, needsSubstitution.StatusCode);
        Assert.Contains("codePrefixFrom", await DetailAsync(needsSubstitution), StringComparison.Ordinal);
        Assert.Equal((aisle.Id, source.Id, "A02", "Aisle 2", "A"), (within.CopiedFromId, within.SiteId, within.Code, within.Name, within.WalkOrderPrefix));
        Assert.Equal(["A-02-01", "A-02-02"], withinLocations.Select(l => l.Code));
        Assert.Equal(["A-02-01-BC", "A-02-02-BC"], withinLocations.Select(l => l.Barcode));
        Assert.Equal("00:35:00", await ValueAsync(services, SettingScopeRef.Zone(within.Id)));
        Assert.Equal((aisle.Id, target.Id, "A09"), (across.CopiedFromId, across.SiteId, across.Code));
        Assert.Equal("00:35:00", await ValueAsync(services, SettingScopeRef.Zone(across.Id)));
        Assert.Equal(HttpStatusCode.Conflict, takenCode.StatusCode);
        Assert.Equal("copies.code_taken", await CodeAsync(takenCode));
        Assert.Equal(HttpStatusCode.NotFound, unknownTarget.StatusCode);
    }



    private static async Task AssertLocationCopiesAsync(HttpClient client, SiteInfo source, ZoneInfo aisle, LocationInfo bin1)
    {
        var single = await PostAsync<LocationInfo>(client, $"/api/v0/sites/{source.Id}/locations/{bin1.Id}/copy", new LocationCopyRequest("A-01-09", "A-01-09-BC", "A-0109"));
        using var takenCode = await client.PostAsync(new Uri($"/api/v0/sites/{source.Id}/locations/{bin1.Id}/copy", UriKind.Relative), Body(new LocationCopyRequest("a-01-09", "X-BC")));
        using var takenBarcode = await client.PostAsync(new Uri($"/api/v0/sites/{source.Id}/locations/{bin1.Id}/copy", UriKind.Relative), Body(new LocationCopyRequest("A-01-10", "A-01-09-BC")));
        using var unknownZone = await client.PostAsync(new Uri($"/api/v0/sites/{source.Id}/locations/{bin1.Id}/copy", UriKind.Relative), Body(new LocationCopyRequest("A-01-10", "A-01-10-BC", ZoneId: aisle.Id + 1000)));
        var range = await PostAsync<List<LocationInfo>>(client, $"/api/v0/sites/{source.Id}/locations/copy-range", new LocationRangeCopyRequest("A-01", "A-03", WalkPrefixFrom: "A-01", WalkPrefixTo: "A-03"));
        using var rangeAgain = await client.PostAsync(new Uri($"/api/v0/sites/{source.Id}/locations/copy-range", UriKind.Relative), Body(new LocationRangeCopyRequest("A-01", "A-03")));
        using var noMatch = await client.PostAsync(new Uri($"/api/v0/sites/{source.Id}/locations/copy-range", UriKind.Relative), Body(new LocationRangeCopyRequest("Z-", "Y-")));
        var all = (await client.GetFromJsonAsync<Core.Http.Paging.Page<LocationInfo>>($"/api/v0/sites/{source.Id}/locations?size=100", Json))!.Items;

        Assert.Equal((bin1.Id, "A-01-09", "A-01-09-BC", "A-0109", aisle.Id), (single.CopiedFromId, single.Code, single.Barcode, single.WalkSequence, single.ZoneId));
        Assert.Equal(HttpStatusCode.Conflict, takenCode.StatusCode);
        Assert.Equal("copies.code_taken", await CodeAsync(takenCode));
        Assert.Equal(HttpStatusCode.Conflict, takenBarcode.StatusCode);
        Assert.Equal("copies.barcode_taken", await CodeAsync(takenBarcode));
        Assert.Equal(HttpStatusCode.NotFound, unknownZone.StatusCode);
        Assert.Equal(["A-03-01", "A-03-02", "A-03-09"], range.Select(l => l.Code));
        Assert.Equal(["A-0301", "A-0302", "A-0309"], range.Select(l => l.WalkSequence));
        Assert.All(range, l => Assert.NotNull(l.CopiedFromId));
        Assert.Equal(HttpStatusCode.Conflict, rangeAgain.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noMatch.StatusCode);
        Assert.Equal(8, all.Count);   // 2 originals + 2 from the zone copy + 1 single + 3 from the range
    }



    private static async Task AssertAuditAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var copiedFrom = await context.Set<AuditDetail>().Where(d => d.ColumnName == "copied_from_id" && d.ValueText != null).CountAsync();

        Assert.True(copiedFrom >= 12, $"expected every copied site, zone and location create to be audited with its copied-from id; found {copiedFrom}");
    }



    private static async Task SetAsync(IServiceProvider services, SettingScopeRef scope, TimeSpan value)
    {
        using var serviceScope = services.CreateScope();
        await serviceScope.ServiceProvider.GetRequiredService<ISettings>().SetAsync(LeaseTimeout, scope, value, "admin", CancellationToken.None);
    }



    private static async Task<string> ValueAsync(IServiceProvider services, SettingScopeRef scope)
    {
        using var serviceScope = services.CreateScope();
        var values = await serviceScope.ServiceProvider.GetRequiredService<ISettings>().ListAsync(scope, CancellationToken.None);
        return values.Single(v => string.Equals(v.Name, LeaseTimeout.Name, StringComparison.Ordinal)).EffectiveValue;
    }



    private static async Task<long> AdminIdAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<WmsDbContext>().Users.OrderBy(u => u.Id).Select(u => u.Id).FirstAsync();
    }



    private static async Task<T> PostAsync<T>(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsync(new Uri(path, UriKind.Relative), Body(body));
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{path}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }



    private static StringContent Body(object value)
    {
        return new StringContent(JsonSerializer.Serialize(value, value.GetType(), Json), Encoding.UTF8, "application/json");
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
        });
        builder.Services.AddWmsApiVersioning();
        builder.Services.AddWmsProblemDetails();
        builder.Services.AddWmsModules();
        builder.Services.AddWmsSettingsModule();
        builder.Services.AddWmsAuthModule();
        builder.Services.AddWmsSitesModule();
        builder.Services.AddWmsZonesModule();
        builder.Services.AddWmsLocationsModule();
        builder.Services.AddWmsCopiesModule();
        builder.Services.AddWmsModule(ModuleDescriptor.Create("sample").WithSettings(LeaseTimeout));
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
