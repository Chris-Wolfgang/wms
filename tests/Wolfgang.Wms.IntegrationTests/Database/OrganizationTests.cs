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
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Organization;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.IntegrationTests.Api;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E16.0 against real engines through the API: nothing exists on a fresh install (404 on both views); the
/// administrator creates the organisation once (a second attempt is 409); the anonymous view carries the
/// name and the logo only; an update needs <c>If-Match</c> (428 without, 412 stale, 200 current) and bumps
/// the version; an invalid draft is 400 with the reason; every write is audited with the user.
/// </summary>
public sealed class OrganizationTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private static readonly OrganizationDraft Draft = new
    (
        "Acme Logistics",
        "Acme Logistics GmbH",
        "data:image/png;base64,iVBORw0KGgo=",
        "Europe/Berlin",
        "de-DE",
        new OrganizationAddress("Industriestraße 1", null, "Hamburg", null, "20457", "Germany"),
        new OrganizationContact("Operations", "ops@acme.example", "+49 40 123456"),
        null
    );



    [SqlServerFact]
    public async Task SqlServer_creates_reads_and_updates_the_one_organization()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();

        await AssertOrganizationAsync("SqlServer", database.ConnectionString, trustServerCertificate: true);
    }



    [DockerFact]
    public async Task PostgreSql_creates_reads_and_updates_the_one_organization()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertOrganizationAsync("PostgreSql", container.GetConnectionString(), trustServerCertificate: false);
    }



    private static async Task AssertOrganizationAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        await TestMigrations.ApplyAsync(provider, connectionString);
        var runtime = await TestLogins.CreateRuntimeAsync(provider, connectionString);

        await using var app = await StartHostAsync(provider, runtime, trustServerCertificate);
        using var anonymous = app.GetTestClient();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", await TestSessions.SignInAsAdministratorAsync(client));

        using var nothingYet = await anonymous.GetAsync(new Uri("/api/v0/organization/public", UriKind.Relative));
        using var nothingYetSignedIn = await client.GetAsync(new Uri("/api/v0/organization", UriKind.Relative));
        using var created = await client.PostAsync(new Uri("/api/v0/organization", UriKind.Relative), Body(Draft));
        using var again = await client.PostAsync(new Uri("/api/v0/organization", UriKind.Relative), Body(Draft));
        using var invalid = await client.PostAsync(new Uri("/api/v0/organization", UriKind.Relative), Body(Draft with { TimeZone = "Mars/Olympus" }));
        var createdInfo = (await created.Content.ReadFromJsonAsync<OrganizationInfo>(Json))!;
        var publicView = await anonymous.GetFromJsonAsync<OrganizationPublicInfo>("/api/v0/organization/public", Json);
        using var read = await client.GetAsync(new Uri("/api/v0/organization", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, nothingYet.StatusCode);
        Assert.Equal("organization.not_created", await CodeAsync(nothingYet));
        Assert.Equal(HttpStatusCode.NotFound, nothingYetSignedIn.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(createdInfo.Etag, created.Headers.ETag!.ToString());
        Assert.Equal(("Acme Logistics", "Acme Logistics GmbH", "Europe/Berlin", "de-DE", "admin"), (createdInfo.Name, createdInfo.LegalName, createdInfo.TimeZone, createdInfo.Locale, createdInfo.UpdatedBy));
        Assert.Equal(Draft.Address, createdInfo.Address);
        Assert.Equal(Draft.PrimaryContact, createdInfo.PrimaryContact);
        Assert.Null(createdInfo.SupportContact);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("organization.already_exists", await CodeAsync(again));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("organization.invalid", await CodeAsync(invalid));
        Assert.Equal(new OrganizationPublicInfo("Acme Logistics", Draft.LogoDataUrl), publicView);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(createdInfo.Etag, read.Headers.ETag!.ToString());

        await AssertUpdateAsync(client, createdInfo);
        await AssertAuditAsync(app.Services);
    }



    private static async Task AssertUpdateAsync(HttpClient client, OrganizationInfo created)
    {
        var renamed = Draft with { Name = "Acme Logistics Europe", SupportContact = new OrganizationContact("Service desk", "help@acme.example", null) };

        using var noPrecondition = await client.PutAsync(new Uri("/api/v0/organization", UriKind.Relative), Body(renamed));
        using var updated = await client.SendAsync(Put(renamed, created.Etag));
        var updatedInfo = (await updated.Content.ReadFromJsonAsync<OrganizationInfo>(Json))!;
        using var stale = await client.SendAsync(Put(renamed, created.Etag));
        using var invalid = await client.SendAsync(Put(renamed with { Locale = "xx-NOPE" }, updatedInfo.Etag));

        Assert.Equal(HttpStatusCode.PreconditionRequired, noPrecondition.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Acme Logistics Europe", updatedInfo.Name);
        Assert.Equal(renamed.SupportContact, updatedInfo.SupportContact);
        Assert.True(updatedInfo.RowVersion > created.RowVersion);
        Assert.Equal(updatedInfo.Etag, updated.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }



    private static async Task AssertAuditAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var headers = await context.Set<AuditHeader>().Include(h => h.Details).Where(h => h.EntityTable.Contains("organization")).ToListAsync();
        var row = await context.Organizations.SingleAsync();

        Assert.True(headers.Count >= 2, $"expected the create and the update to be audited; found {headers.Count}");
        Assert.Contains(headers.SelectMany(h => h.Details), d => string.Equals(d.ColumnName, "name", StringComparison.Ordinal) && string.Equals(d.ValueText, "Acme Logistics Europe", StringComparison.Ordinal));
        Assert.Equal("admin", row.UpdatedBy);
    }



    private static HttpRequestMessage Put(OrganizationDraft draft, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, new Uri("/api/v0/organization", UriKind.Relative)) { Content = Body(draft) };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return request;
    }



    private static StringContent Body(OrganizationDraft draft)
    {
        return new StringContent(JsonSerializer.Serialize(draft, Json), Encoding.UTF8, "application/json");
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
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
        builder.Services.AddWmsOrganizationModule();
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
