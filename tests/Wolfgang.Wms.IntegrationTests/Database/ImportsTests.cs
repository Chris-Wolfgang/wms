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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Wolfgang.AuditTrail.Entities;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Imports;
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
/// E16.6 against real engines through the API: a zones file with a bad row is rolled back under
/// <c>all_or_nothing</c> and loaded minus the bad rows under <c>accept_valid_rows</c>; re-running a file is
/// unchanged; <c>validate_only</c> predicts updates, deletes, duplicates and unknown keys without writing;
/// the same file then writes them; a resolution zone is never touched; retiring a zone with open groups fails
/// the row; the rows come back as CSV; a locations file resolves zones by code, refuses unknown zones,
/// duplicate barcodes in the file, a walk sequence outside the zone's prefix and a barcode another bin holds;
/// imported zones get their settings scope; every write is audited; an unknown site is 404.
/// </summary>
public sealed class ImportsTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private static readonly SettingKey<TimeSpan> LeaseTimeout = new("sample.lease_timeout", TimeSpan.FromMinutes(15), "How long a picker holds a task.");



    [SqlServerFact]
    public async Task SqlServer_imports_zones_and_locations_under_each_policy()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();

        await AssertImportsAsync("SqlServer", database.ConnectionString, trustServerCertificate: true);
    }



    [DockerFact]
    public async Task PostgreSql_imports_zones_and_locations_under_each_policy()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertImportsAsync("PostgreSql", container.GetConnectionString(), trustServerCertificate: false);
    }



    private static async Task AssertImportsAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        await TestMigrations.ApplyAsync(provider, connectionString);
        var runtime = await TestLogins.CreateRuntimeAsync(provider, connectionString);

        var groups = new FakeOpenZoneGroups();
        await using var app = await StartHostAsync(provider, runtime, trustServerCertificate, groups);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", await TestSessions.SignInAsAdministratorAsync(client));
        var site = await CreateSiteAsync(client, "DC1");
        await CreateResolutionZoneAsync(client, site.Id);

        using var unknownSite = await client.PostAsync(Zones(site.Id + 100, null), Body(new[] { new ZoneImportRow("A01", "Aisle 1", ZoneType.Pick, ImportAction.Upsert) }));
        Assert.Equal(HttpStatusCode.NotFound, unknownSite.StatusCode);
        Assert.Equal("imports.site_not_found", await CodeAsync(unknownSite));

        await AssertZonesAsync(client, app.Services, site.Id, groups);
        await AssertLocationsAsync(client, app.Services, site.Id);
        await AssertAuditAsync(app.Services);
    }



    private static async Task AssertZonesAsync(HttpClient client, IServiceProvider services, long siteId, FakeOpenZoneGroups groups)
    {
        var mixed = new[]
        {
            new ZoneImportRow("A01", "Aisle 1", ZoneType.Pick, ImportAction.Upsert, "A"),
            new ZoneImportRow("B01", "Bulk 1", ZoneType.Bulk, ImportAction.Upsert),
            new ZoneImportRow("RES", "Resolution lane", ZoneType.Pick, ImportAction.Upsert),
            new ZoneImportRow("C01", "Bad", ZoneType.Bulk, ImportAction.Upsert, IsRejectLane: true),
        };

        var rolledBack = await ImportAsync(client, Zones(siteId, null), mixed);
        var countAfterRollback = await ZoneCountAsync(services, siteId);
        var partial = await ImportAsync(client, Zones(siteId, "accept_valid_rows"), mixed);
        var a01 = await ZoneIdAsync(services, siteId, "A01");
        var populated = await PopulatedAsync(services, a01);
        var again = await ImportAsync(client, Zones(siteId, "accept_valid_rows"), mixed[..2]);

        Assert.Equal((ImportPolicy.AllOrNothing, false, 2, 2, 1), (rolledBack.Policy, rolledBack.Written, rolledBack.Inserted, rolledBack.Failed, countAfterRollback));
        Assert.Equal(["Inserted", "Inserted", "Failed", "Failed"], rolledBack.Rows.Select(r => r.Outcome.ToString()));
        Assert.Equal(["imports.resolution_zone", "zones.invalid"], rolledBack.Rows.Where(r => r.Outcome == ImportRowOutcome.Failed).Select(r => r.Code));
        Assert.Equal((true, 2, 2), (partial.Written, partial.Inserted, partial.Failed));
        Assert.True(populated);
        Assert.Equal((false, 2), (again.Written, again.Unchanged));

        var changes = new[]
        {
            new ZoneImportRow("A01", "Aisle one", ZoneType.Pick, ImportAction.Upsert, "A"),
            new ZoneImportRow("B01", "Bulk 1", ZoneType.Bulk, ImportAction.Delete),
            new ZoneImportRow("a01", "Aisle 1 again", ZoneType.Pick, ImportAction.Upsert),
            new ZoneImportRow("D01", "Nope", ZoneType.Pick, ImportAction.Delete),
        };

        var predicted = await ImportAsync(client, Zones(siteId, "validate_only"), changes);
        var nameBefore = await ZoneNameAsync(services, siteId, "A01");
        var applied = await ImportAsync(client, Zones(siteId, "accept_valid_rows"), changes);
        var nameAfter = await ZoneNameAsync(services, siteId, "A01");
        groups.Open[a01] = 1;
        var blocked = await ImportAsync(client, Zones(siteId, "accept_valid_rows"), [new ZoneImportRow("A01", "Aisle one", ZoneType.Pick, ImportAction.Upsert, "A", IsActive: false)]);
        using var csv = await client.PostAsync(Zones(siteId, "validate_only&format=csv"), Body(changes));
        var csvText = await csv.Content.ReadAsStringAsync();

        Assert.Equal((false, 1, 1, 2), (predicted.Written, predicted.Updated, predicted.Deleted, predicted.Failed));
        Assert.Equal(["imports.duplicate_in_file", "imports.key_not_found"], predicted.Rows.Where(r => r.Outcome == ImportRowOutcome.Failed).Select(r => r.Code));
        Assert.Equal("Aisle 1", nameBefore);
        Assert.Equal((true, 1, 1, 2), (applied.Written, applied.Updated, applied.Deleted, applied.Failed));
        Assert.Equal("Aisle one", nameAfter);
        Assert.Equal((false, "zones.has_open_groups"), (blocked.Written, blocked.Rows.Single().Code));
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.StartsWith("text/csv", csv.Content.Headers.ContentType!.MediaType, StringComparison.Ordinal);
        Assert.StartsWith("row,key,outcome,code,message\r\n1,A01,Unchanged,,\r\n2,B01,Unchanged,,\r\n3,a01,Failed,imports.duplicate_in_file,", csvText, StringComparison.Ordinal);   // the changes were applied just before, so the re-run predicts nothing to do
        Assert.Equal(5, csvText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }



    private static async Task AssertLocationsAsync(HttpClient client, IServiceProvider services, long siteId)
    {
        var first = new[]
        {
            new LocationImportRow("A-01-01", "L1", "A01", "A-0101", ImportAction.Upsert),
            new LocationImportRow("A-01-02", "L2", "a01", "A-0102", ImportAction.Upsert),
            new LocationImportRow("X-01", "L3", "Z99", "X-0101", ImportAction.Upsert),
            new LocationImportRow("A-01-03", "L1", "A01", "A-0103", ImportAction.Upsert),
            new LocationImportRow("A-01-04", "L4", "A01", "B-0104", ImportAction.Upsert),
            new LocationImportRow("B-01-01", "L5", "B01", "B-0101", ImportAction.Upsert),
        };

        var loaded = await ImportAsync(client, Locations(siteId, null), first);
        var again = await ImportAsync(client, Locations(siteId, null), first);
        var changes = new[]
        {
            new LocationImportRow("A-01-01", "L9", "A01", "A-0101", ImportAction.Upsert),
            new LocationImportRow("A-01-02", "L2", "A01", "A-0102", ImportAction.Delete),
            new LocationImportRow("NEW-1", "L5", "A01", "A-0199", ImportAction.Upsert),
        };
        var refused = await ImportAsync(client, Locations(siteId, "all_or_nothing"), changes);
        var barcodeBefore = await BarcodeAsync(services, siteId, "A-01-01");
        var applied = await ImportAsync(client, Locations(siteId, "accept_valid_rows"), changes);
        var barcodeAfter = await BarcodeAsync(services, siteId, "A-01-01");
        var retired = await IsActiveAsync(services, siteId, "A-01-02");

        Assert.Equal((ImportPolicy.AcceptValidRows, true, 3, 3), (loaded.Policy, loaded.Written, loaded.Inserted, loaded.Failed));
        Assert.Equal(["imports.reference_not_found", "imports.duplicate_in_file", "locations.invalid"], loaded.Rows.Where(r => r.Outcome == ImportRowOutcome.Failed).Select(r => r.Code));
        Assert.Contains("walk-order prefix 'A'", loaded.Rows[4].Message, StringComparison.Ordinal);
        Assert.Equal((false, 3, 3), (again.Written, again.Unchanged, again.Failed));
        Assert.Equal((false, 1, 1, 1), (refused.Written, refused.Updated, refused.Deleted, refused.Failed));
        Assert.Equal("locations.barcode_taken", refused.Rows[2].Code);
        Assert.Equal("L1", barcodeBefore);
        Assert.Equal((true, 1, 1, 1), (applied.Written, applied.Updated, applied.Deleted, applied.Failed));
        Assert.Equal("L9", barcodeAfter);
        Assert.False(retired);
    }



    private static async Task AssertAuditAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var zoneHeaders = await context.Set<AuditHeader>().Where(h => h.EntityTable.Contains("zone")).CountAsync();
        var locationHeaders = await context.Set<AuditHeader>().Where(h => h.EntityTable.Contains("location")).CountAsync();
        var rows = await context.Locations.Select(l => l.UpdatedBy).Distinct().ToListAsync();

        Assert.True(zoneHeaders >= 5, $"expected the resolution zone, two inserts, an update and a delete to be audited; found {zoneHeaders}");
        Assert.True(locationHeaders >= 5, $"expected three inserts, an update and a delete to be audited; found {locationHeaders}");
        Assert.Equal(["admin"], rows);
    }



    private static async Task<ImportResult> ImportAsync<T>(HttpClient client, Uri route, IReadOnlyList<T> rows)
    {
        using var response = await client.PostAsync(route, Body(rows));
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{route}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<ImportResult>(Json))!;
    }



    private static async Task<int> ZoneCountAsync(IServiceProvider services, long siteId)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<WmsDbContext>().Zones.CountAsync(z => z.SiteId == siteId);
    }



    private static async Task<long> ZoneIdAsync(IServiceProvider services, long siteId, string code)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<WmsDbContext>().Zones.Where(z => z.SiteId == siteId && z.CodeNormalized == code).Select(z => z.Id).SingleAsync();
    }



    private static async Task<string> ZoneNameAsync(IServiceProvider services, long siteId, string code)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<WmsDbContext>().Zones.Where(z => z.SiteId == siteId && z.CodeNormalized == code).Select(z => z.Name).SingleAsync();
    }



    private static async Task<bool> PopulatedAsync(IServiceProvider services, long zoneId)
    {
        using var scope = services.CreateScope();
        var values = await scope.ServiceProvider.GetRequiredService<ISettings>().ListAsync(SettingScopeRef.Zone(zoneId), CancellationToken.None);
        return values.Any(v => string.Equals(v.Name, "sample.lease_timeout", StringComparison.Ordinal));
    }



    private static async Task<string> BarcodeAsync(IServiceProvider services, long siteId, string code)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<WmsDbContext>().Locations.Where(l => l.SiteId == siteId && l.CodeNormalized == code).Select(l => l.Barcode).SingleAsync();
    }



    private static async Task<bool> IsActiveAsync(IServiceProvider services, long siteId, string code)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<WmsDbContext>().Locations.Where(l => l.SiteId == siteId && l.CodeNormalized == code).Select(l => l.IsActive).SingleAsync();
    }



    private static async Task<SiteInfo> CreateSiteAsync(HttpClient client, string code)
    {
        using var response = await client.PostAsync(new Uri("/api/v0/sites", UriKind.Relative), Body(new SiteDraft(code, code, "UTC")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SiteInfo>(Json))!;
    }



    private static async Task CreateResolutionZoneAsync(HttpClient client, long siteId)
    {
        var draft = new ZoneDraft("RES", "Resolution lane", ZoneType.Resolution, null, IsRejectLane: false, new ResolutionZone(null, null, [], true, true, true, true, false));
        using var response = await client.PostAsync(new Uri($"/api/v0/sites/{siteId}/zones", UriKind.Relative), Body(draft));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }



    private static Uri Zones(long siteId, string? query)
    {
        return new Uri($"/api/v0/sites/{siteId}/imports/zones" + (query is null ? string.Empty : "?policy=" + query), UriKind.Relative);
    }



    private static Uri Locations(long siteId, string? query)
    {
        return new Uri($"/api/v0/sites/{siteId}/imports/locations" + (query is null ? string.Empty : "?policy=" + query), UriKind.Relative);
    }



    private static StringContent Body<T>(T value)
    {
        return new StringContent(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }



    private static async Task<WebApplication> StartHostAsync(string provider, string connectionString, bool trustServerCertificate, IOpenZoneGroups groups)
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
        builder.Services.AddWmsImportsModule();
        builder.Services.AddWmsModule(ModuleDescriptor.Create("sample").WithSettings(LeaseTimeout));
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        builder.Services.RemoveAll<IOpenZoneGroups>();
        builder.Services.AddSingleton(groups);
        var app = builder.Build();
        app.UseWmsProblemDetails();
        app.UseWmsAuth();
        app.MapWmsApi().MapWmsModules();
        await app.StartAsync();
        return app;
    }



    private sealed class FakeOpenZoneGroups : IOpenZoneGroups
    {
        public Dictionary<long, int> Open { get; } = [];



        public Task<int> CountOpenAsync(long zoneId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Open.GetValueOrDefault(zoneId));
        }
    }
}
