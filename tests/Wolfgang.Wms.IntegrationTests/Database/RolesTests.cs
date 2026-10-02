// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Identity;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.IntegrationTests.Api;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E10.2/E10.3 against PostgreSQL through the API: the built-in roles are seeded from the catalog and the
/// administrator holds Administrator; roles are created from catalog permissions only, built-in ones are
/// read-only but copyable; a site-scoped assignment grants at that site only and an expired one grants
/// nothing; assignments and custom roles can be removed.
/// </summary>
public sealed class RolesTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;



    [DockerFact]
    public async Task Built_in_roles_custom_roles_and_site_scoped_assignments()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();
        await using var app = await StartHostAsync(container.GetConnectionString());
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", await TestSessions.SignInAsAdministratorAsync(client));

        var roles = await AssertBuiltInRolesAsync(client, app.Services);
        var custom = await AssertRoleManagementAsync(client, roles);
        await AssertAssignmentsAsync(client, app, roles, custom);
    }



    private static async Task<Dictionary<string, RoleInfo>> AssertBuiltInRolesAsync(HttpClient client, IServiceProvider services)
    {
        var roles = (await client.GetFromJsonAsync<List<RoleInfo>>("/api/v0/auth/roles", Json))!.ToDictionary(r => r.Name, StringComparer.Ordinal);
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var admin = await context.Users.SingleAsync(u => u.IsLocalAdmin);
        var adminAssignments = await client.GetFromJsonAsync<List<RoleAssignmentInfo>>($"/api/v0/auth/users/{admin.Id}/roles", Json);

        Assert.Equal(["Administrator", "Supervisor", "Resolver", "Support", "Viewer"], roles.Values.Where(r => r.BuiltIn is not null).Select(r => r.Name));
        Assert.Equal(["*"], roles["Administrator"].Permissions);
        Assert.Equal(["settings.read", "workspace.insights.enter", "workspace.report.enter", "workspace.resolve.enter", "workspace.supervise.enter"], roles["Supervisor"].Permissions);
        Assert.Equal(["workspace.resolve.enter"], roles["Resolver"].Permissions);
        Assert.Equal(["auth.roles.read", "settings.read", "workspace.report.enter"], roles["Support"].Permissions);
        Assert.Equal(["settings.read", "workspace.insights.enter", "workspace.report.enter"], roles["Viewer"].Permissions);
        Assert.Contains(adminAssignments!, a => a.RoleId == roles["Administrator"].Id && a.SiteId is null);
        return roles;
    }



    private static async Task<RoleInfo> AssertRoleManagementAsync(HttpClient client, Dictionary<string, RoleInfo> roles)
    {
        using var unknown = await client.PostAsync(new Uri("/api/v0/auth/roles", UriKind.Relative), Body(new RoleDraft("Site lead", "Leads a site", ["settings.read", "nope.nothing"])));
        using var invalid = await client.PostAsync(new Uri("/api/v0/auth/roles", UriKind.Relative), Body(new RoleDraft(" ", "Leads a site", [])));
        using var created = await client.PostAsync(new Uri("/api/v0/auth/roles", UriKind.Relative), Body(new RoleDraft("Site lead", "Leads a site", ["settings.read", "workspace.supervise.enter"])));
        using var duplicate = await client.PostAsync(new Uri("/api/v0/auth/roles", UriKind.Relative), Body(new RoleDraft("site LEAD", "Again", [])));
        var custom = (await created.Content.ReadFromJsonAsync<RoleInfo>(Json))!;
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal("auth.unknown_permission", await CodeAsync(unknown));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(custom.Etag, created.Headers.ETag?.ToString());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("auth.role_name_taken", await CodeAsync(duplicate));

        using var editBuiltIn = await client.SendAsync(Request(HttpMethod.Put, $"/api/v0/auth/roles/{roles["Supervisor"].Id}", Body(new RoleDraft("Supervisor", "x", [])), roles["Supervisor"].Etag));
        using var deleteBuiltIn = await client.DeleteAsync(new Uri($"/api/v0/auth/roles/{roles["Supervisor"].Id}", UriKind.Relative));
        using var noIfMatch = await client.SendAsync(Request(HttpMethod.Put, $"/api/v0/auth/roles/{custom.Id}", Body(new RoleDraft("Site lead", "Leads one site", ["settings.read"])), ifMatch: null));
        using var edited = await client.SendAsync(Request(HttpMethod.Put, $"/api/v0/auth/roles/{custom.Id}", Body(new RoleDraft("Site lead", "Leads one site", ["settings.read"])), custom.Etag));
        using var copied = await client.PostAsync(new Uri($"/api/v0/auth/roles/{roles["Administrator"].Id}/copy", UriKind.Relative), Body(new CopyRoleRequest("Almost admin")));
        using var missing = await client.GetAsync(new Uri("/api/v0/auth/roles/999999", UriKind.Relative));
        var copy = (await copied.Content.ReadFromJsonAsync<RoleInfo>(Json))!;
        Assert.Equal(HttpStatusCode.Conflict, editBuiltIn.StatusCode);
        Assert.Equal("auth.built_in_role_read_only", await CodeAsync(editBuiltIn));
        Assert.Equal(HttpStatusCode.Conflict, deleteBuiltIn.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, noIfMatch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal(["settings.read"], (await edited.Content.ReadFromJsonAsync<RoleInfo>(Json))!.Permissions);
        Assert.Equal(HttpStatusCode.Created, copied.StatusCode);
        Assert.Null(copy.BuiltIn);
        Assert.DoesNotContain("*", copy.Permissions);
        Assert.Contains("settings.write", copy.Permissions);   // the administrator's copy lists every permission explicitly
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        return (await client.GetFromJsonAsync<RoleInfo>($"/api/v0/auth/roles/{custom.Id}", Json))!;
    }



    private static async Task AssertAssignmentsAsync(HttpClient client, WebApplication app, Dictionary<string, RoleInfo> roles, RoleInfo custom)
    {
        var userId = await CreateUserAsync(app.Services, "lead", "a-long-enough-password");
        using var noSuchUser = await client.PostAsync(new Uri("/api/v0/auth/users/999999/roles", UriKind.Relative), Body(new AssignRoleRequest(custom.Id)));
        using var noSuchRole = await client.PostAsync(new Uri($"/api/v0/auth/users/{userId}/roles", UriKind.Relative), Body(new AssignRoleRequest(999999)));
        using var atSite = await client.PostAsync(new Uri($"/api/v0/auth/users/{userId}/roles", UriKind.Relative), Body(new AssignRoleRequest(custom.Id, SiteId: 3)));
        using var expired = await client.PostAsync(new Uri($"/api/v0/auth/users/{userId}/roles", UriKind.Relative), Body(new AssignRoleRequest(roles["Supervisor"].Id, SiteId: null, ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-1))));
        var assignment = (await atSite.Content.ReadFromJsonAsync<RoleAssignmentInfo>(Json))!;
        Assert.Equal(HttpStatusCode.NotFound, noSuchUser.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noSuchRole.StatusCode);
        Assert.Equal(HttpStatusCode.Created, atSite.StatusCode);
        Assert.Equal(HttpStatusCode.Created, expired.StatusCode);
        Assert.Equal((3L, "Site lead", false), (assignment.SiteId, assignment.RoleName, assignment.Expired));

        using var leadClient = app.GetTestClient();   // a second client without the administrator's cookie
        using var leadLogin = await leadClient.PostAsync(new Uri("/api/v0/auth/local/login", UriKind.Relative), Body(new LocalLoginRequest("lead", "a-long-enough-password")));
        var leadCookie = leadLogin.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("wms.session=", StringComparison.Ordinal)).Split(';')[0];
        using var me = await leadClient.SendAsync(Request(HttpMethod.Get, "/api/v0/auth/me", null, null, leadCookie));
        using var elsewhere = await leadClient.SendAsync(Request(HttpMethod.Get, "/api/v0/settings/registry", null, null, leadCookie));
        using var atSite3 = await leadClient.SendAsync(Request(HttpMethod.Get, "/api/v0/settings/registry", null, null, leadCookie, siteId: 3));
        using var atSite4 = await leadClient.SendAsync(Request(HttpMethod.Get, "/api/v0/settings/registry", null, null, leadCookie, siteId: 4));
        var session = (await me.Content.ReadFromJsonAsync<SessionInfo>(Json))!;
        Assert.Equal(HttpStatusCode.OK, leadLogin.StatusCode);
        Assert.Equal(["settings.read@site:3"], session.Permissions);   // the expired Supervisor grants nothing
        Assert.Equal(HttpStatusCode.Forbidden, elsewhere.StatusCode);
        Assert.Equal(HttpStatusCode.OK, atSite3.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, atSite4.StatusCode);

        using var everywhere = await client.PostAsync(new Uri($"/api/v0/auth/users/{userId}/roles", UriKind.Relative), Body(new AssignRoleRequest(roles["Viewer"].Id)));
        using var staleSession = await leadClient.SendAsync(Request(HttpMethod.Get, "/api/v0/auth/me", null, null, leadCookie));
        Assert.Equal(HttpStatusCode.Created, everywhere.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, staleSession.StatusCode);   // E10.5: a role change ends existing sessions

        var listed = (await client.GetFromJsonAsync<List<RoleAssignmentInfo>>($"/api/v0/auth/users/{userId}/roles", Json))!;
        using var unassigned = await client.DeleteAsync(new Uri($"/api/v0/auth/assignments/{assignment.Id}", UriKind.Relative));
        using var unassignAgain = await client.DeleteAsync(new Uri($"/api/v0/auth/assignments/{assignment.Id}", UriKind.Relative));
        using var deleted = await client.DeleteAsync(new Uri($"/api/v0/auth/roles/{custom.Id}", UriKind.Relative));
        Assert.Contains(listed, a => a.Expired && a.RoleId == roles["Supervisor"].Id);
        Assert.Equal(HttpStatusCode.NoContent, unassigned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unassignAgain.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }



    private static async Task<long> CreateUserAsync(IServiceProvider services, string userName, string password)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var user = new User { UserName = userName, UserNameNormalized = EfLocalAccounts.Normalize(userName), DisplayName = userName, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        user.PasswordHash = hasher.HashPassword(user, password);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }



    private static async Task<WebApplication> StartHostAsync(string connectionString)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Wms:Database:Provider"] = "PostgreSql",
            ["Wms:Database:ConnectionString"] = connectionString,
            ["Wms:Database:AutoMigrate"] = "true",
        });
        builder.Services.AddWmsApiVersioning();
        builder.Services.AddWmsProblemDetails();
        builder.Services.AddWmsModules();
        builder.Services.AddWmsSettingsModule();
        builder.Services.AddWmsAuthModule();
        builder.Services.AddWmsRolesModule();
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        var app = builder.Build();
        app.UseWmsProblemDetails();
        app.UseWmsAuth();
        app.MapWmsApi().MapWmsModules();
        await app.StartAsync();
        return app;
    }



    private static StringContent Body<T>(T value)
    {
        return new StringContent(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");
    }



    private static HttpRequestMessage Request(HttpMethod method, string path, HttpContent? content, string? ifMatch, string? cookie = null, long? siteId = null)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative)) { Content = content };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        if (siteId is { } site)
        {
            request.Headers.Add(SiteContext.Header, site.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return request;
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }
}
