// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Core.Zones;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.IntegrationTests.Api;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E16.3 against real engines through the API with the test scheme's grants: an organisation-level reader
/// lists every site; a reader granted at one site lists that site only, at two sites both, with the wildcard
/// at a site that site, and with no site grant nothing; a site-scoped reader can open their own site's zones
/// and is refused another site's (403, the route gate); the stored filter is one translated
/// <c>site_id IN (...)</c>.
/// </summary>
public sealed class SiteScopeTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;



    [SqlServerFact]
    public async Task SqlServer_lists_only_the_sites_in_the_callers_scope()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();

        await AssertScopeAsync("SqlServer", database.ConnectionString, trustServerCertificate: true);
    }



    [DockerFact]
    public async Task PostgreSql_lists_only_the_sites_in_the_callers_scope()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertScopeAsync("PostgreSql", container.GetConnectionString(), trustServerCertificate: false);
    }



    private static async Task AssertScopeAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        await TestMigrations.ApplyAsync(provider, connectionString);
        var runtime = await TestLogins.CreateRuntimeAsync(provider, connectionString);

        await using var app = await StartHostAsync(provider, runtime, trustServerCertificate);
        using var client = app.GetTestClient();
        var a = await CreateSiteAsync(client, "DC-A");
        var b = await CreateSiteAsync(client, "DC-B");
        var c = await CreateSiteAsync(client, "DC-C");
        using var zone = await client.SendAsync(TestAuth.As(HttpMethod.Post, $"/api/v0/sites/{a.Id}/zones", "zones.write@organization", Body(new ZoneDraft("A01", "Aisle 1", ZoneType.Pick, null, IsRejectLane: false, Resolution: null))));

        var everywhere = await ListAsync(client, "sites.read@organization");
        var wildcard = await ListAsync(client, "*@organization");
        var onlyA = await ListAsync(client, $"sites.read@site:{a.Id}");
        var aAndC = await ListAsync(client, $"sites.read@site:{a.Id},sites.read@site:{c.Id},zones.read@site:{b.Id}");
        var wildcardAtB = await ListAsync(client, $"*@site:{b.Id}");
        var retiredStillListed = await ListAsync(client, $"sites.read@site:{c.Id}");
        using var ownZones = await client.SendAsync(TestAuth.As(HttpMethod.Get, $"/api/v0/sites/{a.Id}/zones", $"zones.read@site:{a.Id}"));
        using var otherZones = await client.SendAsync(TestAuth.As(HttpMethod.Get, $"/api/v0/sites/{b.Id}/zones", $"zones.read@site:{a.Id}"));
        using var nothing = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/sites", "zones.read@organization"));

        Assert.Equal(HttpStatusCode.Created, zone.StatusCode);
        Assert.Equal(["DC-A", "DC-B", "DC-C"], everywhere.Select(s => s.Code));
        Assert.Equal(["DC-A", "DC-B", "DC-C"], wildcard.Select(s => s.Code));
        Assert.Equal(["DC-A"], onlyA.Select(s => s.Code));
        Assert.Equal(["DC-A", "DC-C"], aAndC.Select(s => s.Code));
        Assert.Equal(["DC-B"], wildcardAtB.Select(s => s.Code));
        Assert.Equal(["DC-C"], retiredStillListed.Select(s => s.Code));
        Assert.Equal(HttpStatusCode.OK, ownZones.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherZones.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, nothing.StatusCode);
    }



    private static async Task<List<SiteInfo>> ListAsync(HttpClient client, string grants)
    {
        using var response = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/sites", grants));
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{grants}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<List<SiteInfo>>(Json))!;
    }



    private static async Task<SiteInfo> CreateSiteAsync(HttpClient client, string code)
    {
        using var response = await client.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites", "sites.write@organization", Body(new SiteDraft(code, code, "UTC"))));
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{code}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<SiteInfo>(Json))!;
    }



    private static StringContent Body<T>(T draft)
    {
        return new StringContent(JsonSerializer.Serialize(draft, Json), Encoding.UTF8, "application/json");
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
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        builder.Services.AddTestAuth();
        var app = builder.Build();
        app.UseWmsProblemDetails();
        app.UseWmsAuth();
        app.MapWmsApi().MapWmsModules();
        await app.StartAsync();
        return app;
    }
}
