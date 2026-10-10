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
using Wolfgang.Wms.Core.Organization;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.IntegrationTests.Api;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E16.1 against real engines through the API: an empty list and 404 on a fresh install; create, a duplicate
/// code in another case is 409, an invalid draft 400; the list is in code order; an update needs <c>If-Match</c>
/// (428 without, 412 stale, 200 current) and refuses a code another site holds; retiring a site is 409 while
/// <see cref="IOpenReleases"/> reports open releases and succeeds once it does not; the settings cascade
/// reaches the stored sites; a new site's settings scope is populated at once and a draft without a time zone
/// takes the organisation's (E16.4); every write is audited with the user.
/// </summary>
public sealed class SitesTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private static readonly SettingKey<TimeSpan> LeaseTimeout = new("sample.lease_timeout", TimeSpan.FromMinutes(15), "How long a picker holds a task.");



    [SqlServerFact]
    public async Task SqlServer_creates_lists_updates_and_retires_sites()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();

        await AssertSitesAsync("SqlServer", database.ConnectionString, trustServerCertificate: true);
    }



    [DockerFact]
    public async Task PostgreSql_creates_lists_updates_and_retires_sites()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertSitesAsync("PostgreSql", container.GetConnectionString(), trustServerCertificate: false);
    }



    private static async Task AssertSitesAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        await TestMigrations.ApplyAsync(provider, connectionString);
        var runtime = await TestLogins.CreateRuntimeAsync(provider, connectionString);

        var releases = new FakeOpenReleases();
        await using var app = await StartHostAsync(provider, runtime, trustServerCertificate, releases);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", await TestSessions.SignInAsAdministratorAsync(client));

        var empty = await client.GetFromJsonAsync<List<SiteInfo>>("/api/v0/sites", Json);
        using var missing = await client.GetAsync(new Uri("/api/v0/sites/1", UriKind.Relative));
        using var created = await client.PostAsync(new Uri("/api/v0/sites", UriKind.Relative), Body(new SiteDraft("HAM-01", "Hamburg", "Europe/Berlin")));
        using var duplicate = await client.PostAsync(new Uri("/api/v0/sites", UriKind.Relative), Body(new SiteDraft("ham-01", "Hamburg again", "Europe/Berlin")));
        using var invalid = await client.PostAsync(new Uri("/api/v0/sites", UriKind.Relative), Body(new SiteDraft("DC1", "Main", "Mars/Olympus")));
        using var second = await client.PostAsync(new Uri("/api/v0/sites", UriKind.Relative), Body(new SiteDraft("DC1", "Main", "America/Chicago")));
        var hamburg = (await created.Content.ReadFromJsonAsync<SiteInfo>(Json))!;
        var main = (await second.Content.ReadFromJsonAsync<SiteInfo>(Json))!;
        var listed = await client.GetFromJsonAsync<List<SiteInfo>>("/api/v0/sites", Json);
        using var read = await client.GetAsync(new Uri($"/api/v0/sites/{hamburg.Id}", UriKind.Relative));

        Assert.Empty(empty!);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("sites.not_found", await CodeAsync(missing));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(hamburg.Etag, created.Headers.ETag!.ToString());
        Assert.Equal(("HAM-01", "Hamburg", "Europe/Berlin", true, "admin"), (hamburg.Code, hamburg.Name, hamburg.TimeZone, hamburg.IsActive, hamburg.UpdatedBy));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("sites.code_taken", await CodeAsync(duplicate));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("sites.invalid", await CodeAsync(invalid));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(["DC1", "HAM-01"], listed!.Select(s => s.Code));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(hamburg.Etag, read.Headers.ETag!.ToString());

        await AssertUpdateAsync(client, hamburg, main, releases);
        await AssertCascadeAsync(app.Services, hamburg.Id);
        await AssertDefaultsAsync(client, app.Services);
        await AssertAuditAsync(app.Services);
    }



    private static async Task AssertUpdateAsync(HttpClient client, SiteInfo hamburg, SiteInfo main, FakeOpenReleases releases)
    {
        var renamed = new SiteDraft("HAM-01", "Hamburg Nord", "Europe/Berlin");
        var retired = renamed with { IsActive = false };
        releases.Open[hamburg.Id] = 2;

        using var noPrecondition = await client.PutAsync(new Uri($"/api/v0/sites/{hamburg.Id}", UriKind.Relative), Body(renamed));
        using var updated = await client.SendAsync(Put(hamburg.Id, renamed, hamburg.Etag));
        var updatedInfo = (await updated.Content.ReadFromJsonAsync<SiteInfo>(Json))!;
        using var stale = await client.SendAsync(Put(hamburg.Id, renamed, hamburg.Etag));
        using var clash = await client.SendAsync(Put(hamburg.Id, renamed with { Code = "dc1" }, updatedInfo.Etag));
        using var blocked = await client.SendAsync(Put(hamburg.Id, retired, updatedInfo.Etag));
        releases.Open[hamburg.Id] = 0;
        using var retiredNow = await client.SendAsync(Put(hamburg.Id, retired, updatedInfo.Etag));
        var retiredInfo = (await retiredNow.Content.ReadFromJsonAsync<SiteInfo>(Json))!;
        using var unknown = await client.SendAsync(Put(hamburg.Id + main.Id + 100, renamed, "\"1\""));
        var listed = await client.GetFromJsonAsync<List<SiteInfo>>("/api/v0/sites", Json);

        Assert.Equal(HttpStatusCode.PreconditionRequired, noPrecondition.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Hamburg Nord", updatedInfo.Name);
        Assert.True(updatedInfo.RowVersion > hamburg.RowVersion);
        Assert.Equal(updatedInfo.Etag, updated.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Equal("sites.code_taken", await CodeAsync(clash));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("sites.has_open_releases", await CodeAsync(blocked));
        Assert.Equal(HttpStatusCode.OK, retiredNow.StatusCode);
        Assert.False(retiredInfo.IsActive);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal([("DC1", true), ("HAM-01", false)], listed!.Select(s => (s.Code, s.IsActive)));
    }



    private static async Task AssertCascadeAsync(IServiceProvider services, long siteId)
    {
        using var scope = services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettings>();
        var hierarchy = scope.ServiceProvider.GetRequiredService<ISettingScopeHierarchy>();

        var children = await hierarchy.ChildrenAsync(SettingScopeRef.Organization, CancellationToken.None);
        var populatedOnCreate = await settings.ListAsync(SettingScopeRef.Site(siteId), CancellationToken.None);
        var createdNow = await settings.PopulateAsync(SettingScopeRef.Site(siteId), "admin", CancellationToken.None);
        await settings.SetAsync(LeaseTimeout, SettingScopeRef.Organization, TimeSpan.FromMinutes(40), "admin", CancellationToken.None);
        var effective = await settings.GetAsync(LeaseTimeout, SettingScopeRef.Site(siteId), CancellationToken.None);

        Assert.Equal(2, children.Count);
        Assert.Contains(SettingScopeRef.Site(siteId), children);
        Assert.Contains(populatedOnCreate, v => string.Equals(v.Name, "sample.lease_timeout", StringComparison.Ordinal));   // E16.4: the create populated the scope (every module's site-level setting); the explicit populate finds nothing to add
        Assert.Equal(0, createdNow);
        Assert.Equal(TimeSpan.FromMinutes(40), effective);
    }



    private static async Task AssertDefaultsAsync(HttpClient client, IServiceProvider services)
    {
        var organization = new OrganizationDraft("Acme", null, null, "Europe/Berlin", "de-DE", null, null, null);

        using var noTimeZoneYet = await client.PostAsync(new Uri("/api/v0/sites", UriKind.Relative), Body(new SiteDraft("DEF", "Defaults", null)));
        using var created = await client.PostAsync(new Uri("/api/v0/organization", UriKind.Relative), new StringContent(JsonSerializer.Serialize(organization, Json), Encoding.UTF8, "application/json"));
        using var defaulted = await client.PostAsync(new Uri("/api/v0/sites", UriKind.Relative), Body(new SiteDraft("DEF", "Defaults", " ")));
        var site = (await defaulted.Content.ReadFromJsonAsync<SiteInfo>(Json))!;
        using var scope = services.CreateScope();
        var values = await scope.ServiceProvider.GetRequiredService<ISettings>().ListAsync(SettingScopeRef.Site(site.Id), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, noTimeZoneYet.StatusCode);
        Assert.Equal("sites.invalid", await CodeAsync(noTimeZoneYet));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Created, defaulted.StatusCode);
        Assert.Equal("Europe/Berlin", site.TimeZone);
        Assert.Equal("00:40:00", values.Single(v => string.Equals(v.Name, "sample.lease_timeout", StringComparison.Ordinal)).EffectiveValue);   // the organisation's effective value, inherited at creation
    }



    private static async Task AssertAuditAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var headers = await context.Set<AuditHeader>().Include(h => h.Details).Where(h => h.EntityTable.Contains("site")).ToListAsync();
        var rows = await context.Sites.OrderBy(s => s.CodeNormalized).ToListAsync();

        Assert.True(headers.Count >= 4, $"expected two creates and two updates to be audited; found {headers.Count}");
        Assert.Contains(headers.SelectMany(h => h.Details), d => string.Equals(d.ColumnName, "name", StringComparison.Ordinal) && string.Equals(d.ValueText, "Hamburg Nord", StringComparison.Ordinal));
        Assert.All(rows, r => Assert.Equal("admin", r.UpdatedBy));
    }



    private static HttpRequestMessage Put(long siteId, SiteDraft draft, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/v0/sites/{siteId}", UriKind.Relative)) { Content = Body(draft) };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return request;
    }



    private static StringContent Body(SiteDraft draft)
    {
        return new StringContent(JsonSerializer.Serialize(draft, Json), Encoding.UTF8, "application/json");
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }



    private static async Task<WebApplication> StartHostAsync(string provider, string connectionString, bool trustServerCertificate, IOpenReleases releases)
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
        builder.Services.AddWmsOrganizationModule();
        builder.Services.AddWmsSitesModule();
        builder.Services.AddWmsModule(ModuleDescriptor.Create("sample").WithSettings(LeaseTimeout));
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        builder.Services.RemoveAll<IOpenReleases>();
        builder.Services.AddSingleton(releases);
        var app = builder.Build();
        app.UseWmsProblemDetails();
        app.UseWmsAuth();
        app.MapWmsApi().MapWmsModules();
        await app.StartAsync();
        return app;
    }



    private sealed class FakeOpenReleases : IOpenReleases
    {
        public Dictionary<long, int> Open { get; } = [];



        public Task<int> CountOpenAsync(long siteId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Open.GetValueOrDefault(siteId));
        }
    }
}
