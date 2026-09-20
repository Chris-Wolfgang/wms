// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Auth.Oidc;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Identity.External;
using Wolfgang.Wms.Core.Identity.Providers;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Integrity;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.IntegrationTests.Api;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E11.1–E11.3 against PostgreSQL and a mock OpenID Connect provider (navikt/mock-oauth2-server): the
/// provider is configured through settings and enabled without a restart; discovery is the health check;
/// the full authorization-code flow (challenge, provider redirect, callback with the correlation and nonce
/// cookies, back-channel token exchange) ends in a console session whose account was created on first
/// sign-in and whose roles follow the group mappings; removing the mapping removes the role at the next
/// sign-in; a wrong authority fails the check and a wrong state answers a 502 problem.
/// </summary>
public sealed class OidcSignInTests
{
    private const string Issuer = "wms";
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;



    [DockerFact]
    public async Task Configured_from_settings_the_provider_signs_a_directory_user_in_with_mapped_roles()
    {
        await using var database = new PostgreSqlBuilder("postgres:16").Build();
        await using var provider = MockProvider();
        await Task.WhenAll(database.StartAsync(), provider.StartAsync());
        var authority = $"http://{provider.Hostname}:{provider.GetMappedPublicPort(8080)}/{Issuer}";

        await using var app = await StartHostAsync(database.GetConnectionString());
        using var client = app.GetTestClient();
        var admin = await TestSessions.SignInAsAdministratorAsync(client);
        client.DefaultRequestHeaders.Add("Cookie", admin);

        var unconfigured = await CheckAsync(client);
        await ConfigureAsync(app.Services, authority);
        var healthy = await CheckAsync(client);
        var offered = await client.GetFromJsonAsync<List<AuthProviderInfo>>("/api/v0/auth/providers", Json);
        var supervisor = (await client.GetFromJsonAsync<List<RoleInfo>>("/api/v0/auth/roles", Json))!.Single(r => string.Equals(r.Name, "Supervisor", StringComparison.Ordinal));
        using var mapped = await client.PostAsync(new Uri("/api/v0/auth/providers/oidc/groups", UriKind.Relative), Body(new GroupRoleMappingDraft("wms-supervisors", supervisor.Id)));
        using var duplicate = await client.PostAsync(new Uri("/api/v0/auth/providers/oidc/groups", UriKind.Relative), Body(new GroupRoleMappingDraft("wms-supervisors", supervisor.Id)));
        var mappings = await client.GetFromJsonAsync<List<GroupRoleMappingInfo>>("/api/v0/auth/providers/oidc/groups", Json);

        var (session, redirect) = await SignInThroughProviderAsync(app, authority);
        var me = await MeAsync(app, session);
        using var granted = await SettingsRegistryAsync(app, session);

        using var unmapped = await client.DeleteAsync(new Uri($"/api/v0/auth/providers/groups/{mappings![0].Id}", UriKind.Relative));
        var (again, _) = await SignInThroughProviderAsync(app, authority);
        var meAgain = await MeAsync(app, again);
        using var revoked = await SettingsRegistryAsync(app, again);
        using var badState = await BadStateAsync(app, authority);
        await ConfigureAsync(app.Services, "http://127.0.0.1:9/nowhere");
        var unreachable = await CheckAsync(client);

        Assert.False(unconfigured.Healthy);
        Assert.True(healthy.Healthy);
        Assert.Contains("issuer " + authority, healthy.Detail, StringComparison.Ordinal);
        Assert.Equal([("oidc", "Corporate login", "/auth/oidc/challenge"), ("local", "Local account", null)], offered!.Select(p => (p.Name, p.DisplayName, p.ChallengeUrl)));
        Assert.Equal(HttpStatusCode.Created, mapped.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal([("wms-supervisors", "Supervisor", (long?)null)], mappings.Select(m => (m.Group, m.RoleName, m.SiteId)));
        Assert.Equal("/console", redirect);
        Assert.Equal(("alice", "Alice Example", false, false), (me.UserName, me.DisplayName, me.MustChangePassword, me.IsLocalAdmin));
        Assert.Contains("settings.read@organization", me.Permissions);
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unmapped.StatusCode);
        Assert.Equal("alice", meAgain.UserName);
        Assert.Empty(meAgain.Permissions);   // E11.2: unmapped users get no roles
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, badState.StatusCode);
        Assert.Equal("auth.provider_failed", await CodeAsync(badState));
        Assert.False(unreachable.Healthy);
        await AssertAccountAsync(app.Services);
    }



    private static IContainer MockProvider()
    {
        const string config = """
            {
              "interactiveLogin": false,
              "httpServer": "NettyWrapper",
              "tokenCallbacks": [
                {
                  "issuerId": "wms",
                  "tokenExpiry": 3600,
                  "requestMappings": [
                    {
                      "requestParam": "client_id",
                      "match": "wms-console",
                      "claims": { "sub": "alice-sub-1", "preferred_username": "alice", "name": "Alice Example", "groups": ["wms-supervisors", "everyone"] }
                    }
                  ]
                }
              ]
            }
            """;
        return new ContainerBuilder("ghcr.io/navikt/mock-oauth2-server:2.1.10")
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithEnvironment("JSON_CONFIG", config)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/wms/.well-known/openid-configuration")))
            .Build();
    }



    private static async Task ConfigureAsync(IServiceProvider services, string authority)
    {
        using var scope = services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettings>();
        var organization = SettingScopeRef.Organization;
        await settings.SetAsync(OidcSettings.Authority, organization, authority, "test", CancellationToken.None);
        await settings.SetAsync(OidcSettings.ClientId, organization, "wms-console", "test", CancellationToken.None);
        await settings.SetAsync(OidcSettings.ClientSecret, organization, new SecretText("not-checked-by-the-mock"), "test", CancellationToken.None);
        await settings.SetAsync(OidcSettings.DisplayName, organization, "Corporate login", "test", CancellationToken.None);
        await settings.SetAsync(OidcSettings.RequireHttps, organization, false, "test", CancellationToken.None);
        await settings.SetAsync(AuthProviderSettings.Enabled, organization, "oidc,local", "test", CancellationToken.None);
        await services.GetRequiredService<AuthProviderState>().RefreshAsync(CancellationToken.None);   // the sync does this every 5 s
    }



    /// <summary>
    /// The browser's side of the flow: the challenge answers a redirect to the provider and sets the
    /// correlation and nonce cookies; the provider (no interactive login) redirects straight back to the
    /// callback with a code; the callback, sent with those cookies, exchanges the code over the back channel
    /// and answers a redirect to the return URL with the session cookie.
    /// </summary>
    private static async Task<(string Session, string Redirect)> SignInThroughProviderAsync(WebApplication app, string authority)
    {
        using var browser = app.GetTestClient();
        using var challenge = await browser.GetAsync(new Uri("/api/v0/auth/oidc/challenge?returnUrl=/console", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var authorize = challenge.Headers.Location!;
        Assert.StartsWith(authority + "/authorize", authorize.ToString(), StringComparison.Ordinal);
        var cookies = string.Join("; ", challenge.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));

        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var provider = new HttpClient(handler);
        using var atProvider = await provider.GetAsync(authorize);
        Assert.Equal(HttpStatusCode.Redirect, atProvider.StatusCode);
        var callback = atProvider.Headers.Location!;
        Assert.StartsWith("http://localhost/auth/oidc/callback?", callback.ToString(), StringComparison.Ordinal);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(callback.PathAndQuery, UriKind.Relative));
        request.Headers.Add("Cookie", cookies);
        using var signedIn = await browser.SendAsync(request);
        Assert.True(signedIn.StatusCode == HttpStatusCode.Redirect, $"callback answered {(int)signedIn.StatusCode}: {await signedIn.Content.ReadAsStringAsync()}");
        var session = signedIn.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("wms.session=", StringComparison.Ordinal)).Split(';')[0];
        return (session, signedIn.Headers.Location!.ToString());
    }



    private static async Task<HttpResponseMessage> BadStateAsync(WebApplication app, string authority)
    {
        using var browser = app.GetTestClient();
        using var challenge = await browser.GetAsync(new Uri("/api/v0/auth/oidc/challenge", UriKind.Relative));
        var cookies = string.Join("; ", challenge.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/auth/oidc/callback?code=x&state=forged", UriKind.Relative));
        request.Headers.Add("Cookie", cookies);
        _ = authority;
        return await browser.SendAsync(request);
    }



    private static async Task<SessionInfo> MeAsync(WebApplication app, string session)
    {
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v0/auth/me", UriKind.Relative));
        request.Headers.Add("Cookie", session);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionInfo>(Json))!;
    }



    private static async Task<HttpResponseMessage> SettingsRegistryAsync(WebApplication app, string session)
    {
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v0/settings/registry", UriKind.Relative));
        request.Headers.Add("Cookie", session);
        return await client.SendAsync(request);
    }



    private static async Task<AuthProviderHealth> CheckAsync(HttpClient client)
    {
        using var response = await client.PostAsync(new Uri("/api/v0/auth/providers/oidc/check", UriKind.Relative), content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthProviderHealth>(Json))!;
    }



    private static async Task AssertAccountAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var alice = await context.Users.SingleAsync(u => u.UserName == "alice");

        Assert.Equal(("oidc", "alice-sub-1", (string?)null), (alice.Provider, alice.ProviderSubject, alice.PasswordHash));
        Assert.NotNull(alice.Signature);
        Assert.Empty(await context.UserRoles.Where(a => a.UserId == alice.Id).ToListAsync());
        Assert.Equal(0, (await scope.ServiceProvider.GetRequiredService<IntegrityVerificationJob>().RunOnceAsync(CancellationToken.None)).Failed);
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
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
        builder.Services.AddWmsOidcProvider();
        builder.Services.AddWmsRolesModule();
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        builder.Services.AddWmsIntegrityVerification();
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
}
