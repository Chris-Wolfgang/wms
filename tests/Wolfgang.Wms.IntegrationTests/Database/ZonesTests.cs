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
/// E16.2 against real engines through the API: an unknown site is 404 on every zone endpoint; an empty list and
/// 404 before any zone exists; a pick zone with the reject-lane flag, a duplicate code in another case 409, the
/// same code in another site 201, a reject lane on a bulk zone 400; a resolution zone round-trips its properties
/// and resolvers, an unknown resolver is 400; the list is in code order; an update needs <c>If-Match</c>, refuses
/// a code another zone of the site holds, replaces the resolvers; retiring is 409 while <see cref="IOpenZoneGroups"/>
/// reports open groups and succeeds once it does not; the settings cascade reaches the zone through its site;
/// every write is audited with the user.
/// </summary>
public sealed class ZonesTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private static readonly SettingKey<TimeSpan> LeaseTimeout = new("sample.lease_timeout", TimeSpan.FromMinutes(15), "How long a picker holds a task.");
    private static readonly ZoneDraft Pick = new("A01", "Aisle 1", ZoneType.Pick, "A", IsRejectLane: true, Resolution: null);



    [SqlServerFact]
    public async Task SqlServer_creates_lists_updates_and_retires_zones()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();

        await AssertZonesAsync("SqlServer", database.ConnectionString, trustServerCertificate: true);
    }



    [DockerFact]
    public async Task PostgreSql_creates_lists_updates_and_retires_zones()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertZonesAsync("PostgreSql", container.GetConnectionString(), trustServerCertificate: false);
    }



    private static async Task AssertZonesAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        await TestMigrations.ApplyAsync(provider, connectionString);
        var runtime = await TestLogins.CreateRuntimeAsync(provider, connectionString);

        var groups = new FakeOpenZoneGroups();
        await using var app = await StartHostAsync(provider, runtime, trustServerCertificate, groups);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", await TestSessions.SignInAsAdministratorAsync(client));
        var site = await CreateSiteAsync(client, "DC1");
        var other = await CreateSiteAsync(client, "DC2");
        var adminId = await AdminIdAsync(app.Services);
        var resolution = new ZoneDraft("RES-1", "Resolution lane", ZoneType.Resolution, null, IsRejectLane: false, new ResolutionZone("RESTOCK-01", null, [adminId], AcceptsWeightFailures: true, AcceptsShorts: true, AcceptsAdjustments: false, AcceptsMisdirects: true, IsVirtualQueue: false));

        using var unknownSite = await client.GetAsync(new Uri($"/api/v0/sites/{site.Id + other.Id + 100}/zones", UriKind.Relative));
        var empty = await client.GetFromJsonAsync<List<ZoneInfo>>($"/api/v0/sites/{site.Id}/zones", Json);
        using var missing = await client.GetAsync(new Uri($"/api/v0/sites/{site.Id}/zones/1", UriKind.Relative));
        using var created = await client.PostAsync(new Uri($"/api/v0/sites/{site.Id}/zones", UriKind.Relative), Body(Pick));
        using var duplicate = await client.PostAsync(new Uri($"/api/v0/sites/{site.Id}/zones", UriKind.Relative), Body(Pick with { Code = "a01" }));
        using var elsewhere = await client.PostAsync(new Uri($"/api/v0/sites/{other.Id}/zones", UriKind.Relative), Body(Pick));
        using var invalid = await client.PostAsync(new Uri($"/api/v0/sites/{site.Id}/zones", UriKind.Relative), Body(Pick with { Code = "B01", Type = ZoneType.Bulk }));
        using var resolved = await client.PostAsync(new Uri($"/api/v0/sites/{site.Id}/zones", UriKind.Relative), Body(resolution));
        using var unknownResolver = await client.PostAsync(new Uri($"/api/v0/sites/{site.Id}/zones", UriKind.Relative), Body(resolution with { Code = "RES-2", Resolution = resolution.Resolution! with { ResolverUserIds = [adminId + 1000] } }));
        var pick = (await created.Content.ReadFromJsonAsync<ZoneInfo>(Json))!;
        var res = (await resolved.Content.ReadFromJsonAsync<ZoneInfo>(Json))!;
        var listed = await client.GetFromJsonAsync<List<ZoneInfo>>($"/api/v0/sites/{site.Id}/zones", Json);
        using var read = await client.GetAsync(new Uri($"/api/v0/sites/{site.Id}/zones/{pick.Id}", UriKind.Relative));
        using var wrongSite = await client.GetAsync(new Uri($"/api/v0/sites/{other.Id}/zones/{pick.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, unknownSite.StatusCode);
        Assert.Equal("zones.site_not_found", await CodeAsync(unknownSite));
        Assert.Empty(empty!);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("zones.not_found", await CodeAsync(missing));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(pick.Etag, created.Headers.ETag!.ToString());
        Assert.Equal((site.Id, "A01", ZoneType.Pick, "A", true, true, "admin"), (pick.SiteId, pick.Code, pick.Type, pick.WalkOrderPrefix, pick.IsRejectLane, pick.IsActive, pick.UpdatedBy));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("zones.code_taken", await CodeAsync(duplicate));
        Assert.Equal(HttpStatusCode.Created, elsewhere.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("zones.invalid", await CodeAsync(invalid));
        Assert.Equal(HttpStatusCode.Created, resolved.StatusCode);
        Assert.Equal(("RESTOCK-01", (string?)null, true, true, false, true, false), (res.Resolution!.RestockingBin, res.Resolution.ReturnsContainer, res.Resolution.AcceptsWeightFailures, res.Resolution.AcceptsShorts, res.Resolution.AcceptsAdjustments, res.Resolution.AcceptsMisdirects, res.Resolution.IsVirtualQueue));
        Assert.Equal([adminId], res.Resolution.ResolverUserIds);
        Assert.Equal(HttpStatusCode.BadRequest, unknownResolver.StatusCode);
        Assert.Contains("does not exist", await DetailAsync(unknownResolver), StringComparison.Ordinal);
        Assert.Equal(["A01", "RES-1"], listed!.Select(z => z.Code));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(pick.Etag, read.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.NotFound, wrongSite.StatusCode);

        await AssertUpdateAsync(client, site.Id, pick, res, groups);
        await AssertCascadeAsync(app.Services, site.Id, pick.Id);
        await AssertAuditAsync(app.Services);
    }



    private static async Task AssertUpdateAsync(HttpClient client, long siteId, ZoneInfo pick, ZoneInfo res, FakeOpenZoneGroups groups)
    {
        var renamed = Pick with { Name = "Aisle 1 north" };
        var retired = renamed with { IsActive = false };
        var noResolvers = new ZoneDraft(res.Code, res.Name, ZoneType.Resolution, null, IsRejectLane: false, res.Resolution! with { ResolverUserIds = [], IsVirtualQueue = true });
        groups.Open[pick.Id] = 1;

        using var noPrecondition = await client.PutAsync(new Uri($"/api/v0/sites/{siteId}/zones/{pick.Id}", UriKind.Relative), Body(renamed));
        using var updated = await client.SendAsync(Put(siteId, pick.Id, renamed, pick.Etag));
        var updatedInfo = (await updated.Content.ReadFromJsonAsync<ZoneInfo>(Json))!;
        using var stale = await client.SendAsync(Put(siteId, pick.Id, renamed, pick.Etag));
        using var clash = await client.SendAsync(Put(siteId, pick.Id, renamed with { Code = "res-1" }, updatedInfo.Etag));
        using var blocked = await client.SendAsync(Put(siteId, pick.Id, retired, updatedInfo.Etag));
        groups.Open[pick.Id] = 0;
        using var retiredNow = await client.SendAsync(Put(siteId, pick.Id, retired, updatedInfo.Etag));
        var retiredInfo = (await retiredNow.Content.ReadFromJsonAsync<ZoneInfo>(Json))!;
        using var resolvers = await client.SendAsync(Put(siteId, res.Id, noResolvers, res.Etag));
        var resolversInfo = (await resolvers.Content.ReadFromJsonAsync<ZoneInfo>(Json))!;

        Assert.Equal(HttpStatusCode.PreconditionRequired, noPrecondition.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Aisle 1 north", updatedInfo.Name);
        Assert.True(updatedInfo.RowVersion > pick.RowVersion);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Equal("zones.code_taken", await CodeAsync(clash));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("zones.has_open_groups", await CodeAsync(blocked));
        Assert.Equal(HttpStatusCode.OK, retiredNow.StatusCode);
        Assert.False(retiredInfo.IsActive);
        Assert.Equal(HttpStatusCode.OK, resolvers.StatusCode);
        Assert.Empty(resolversInfo.Resolution!.ResolverUserIds);
        Assert.True(resolversInfo.Resolution.IsVirtualQueue);
    }



    private static async Task AssertCascadeAsync(IServiceProvider services, long siteId, long zoneId)
    {
        using var scope = services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettings>();
        var hierarchy = scope.ServiceProvider.GetRequiredService<ISettingScopeHierarchy>();

        var children = await hierarchy.ChildrenAsync(SettingScopeRef.Site(siteId), CancellationToken.None);
        var parent = await hierarchy.ParentAsync(SettingScopeRef.Zone(zoneId), CancellationToken.None);
        var orphan = await hierarchy.ParentAsync(SettingScopeRef.Zone(zoneId + 1000), CancellationToken.None);
        await settings.PopulateAsync(SettingScopeRef.Zone(zoneId), "admin", CancellationToken.None);
        await settings.SetAsync(LeaseTimeout, SettingScopeRef.Organization, TimeSpan.FromMinutes(45), "admin", CancellationToken.None);
        var effective = await settings.GetAsync(LeaseTimeout, SettingScopeRef.Zone(zoneId), CancellationToken.None);

        Assert.Equal(2, children.Count);
        Assert.Contains(SettingScopeRef.Zone(zoneId), children);
        Assert.Equal(SettingScopeRef.Site(siteId), parent);
        Assert.Null(orphan);
        Assert.Equal(TimeSpan.FromMinutes(45), effective);
    }



    private static async Task AssertAuditAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var headers = await context.Set<AuditHeader>().Include(h => h.Details).Where(h => h.EntityTable.Contains("zone")).ToListAsync();
        var rows = await context.Zones.ToListAsync();
        var resolvers = await context.Set<Infrastructure.Zones.ZoneResolver>().CountAsync();

        Assert.True(headers.Count >= 5, $"expected three creates and two updates to be audited; found {headers.Count}");
        Assert.Contains(headers.SelectMany(h => h.Details), d => string.Equals(d.ColumnName, "name", StringComparison.Ordinal) && string.Equals(d.ValueText, "Aisle 1 north", StringComparison.Ordinal));
        Assert.All(rows, r => Assert.Equal("admin", r.UpdatedBy));
        Assert.Equal(0, resolvers);
    }



    private static async Task<SiteInfo> CreateSiteAsync(HttpClient client, string code)
    {
        using var response = await client.PostAsync(new Uri("/api/v0/sites", UriKind.Relative), new StringContent(JsonSerializer.Serialize(new SiteDraft(code, code, "UTC"), Json), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SiteInfo>(Json))!;
    }



    private static async Task<long> AdminIdAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        return await context.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstAsync();
    }



    private static HttpRequestMessage Put(long siteId, long zoneId, ZoneDraft draft, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/v0/sites/{siteId}/zones/{zoneId}", UriKind.Relative)) { Content = Body(draft) };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return request;
    }



    private static StringContent Body(ZoneDraft draft)
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
