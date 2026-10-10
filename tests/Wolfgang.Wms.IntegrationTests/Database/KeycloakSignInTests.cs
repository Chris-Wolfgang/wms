// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
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
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.IntegrationTests.Api;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E11.3 against a real provider: Keycloak from Testcontainers with an imported realm (one confidential client,
/// a group-membership mapper, a supervisor and an unmapped user), on every PR where Docker runs. The console's
/// challenge lands on Keycloak's login page; the page's form is posted with the user's credentials; Keycloak
/// redirects to the callback; the back-channel exchange ends in a session whose roles follow the group mapping.
/// A user in no mapped group signs in with no permissions. Discovery is the health check.
/// </summary>
public sealed partial class KeycloakSignInTests
{
    private const string Realm = "wms";
    private const string ClientId = "wms-console";
    private const string ClientSecret = "wms-console-secret-for-tests";
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    // E3.8 convention: no backtracking, a timeout, explicit capture only.
    private static readonly Regex FormAction = new("<form[^>]*id=\"kc-form-login\"[^>]*action=\"(?<action>[^\"]+)\"", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking, TimeSpan.FromSeconds(1));



    [DockerFact]   // Keycloak is a container, so Docker serves this one either way
    public async Task SqlServer_keycloak_signs_a_realm_user_in_with_mapped_roles_and_an_unmapped_user_without()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();

        await AssertKeycloakSignInAsync("SqlServer", database.ConnectionString, trustServerCertificate: true);
    }



    [DockerFact]
    public async Task PostgreSql_keycloak_signs_a_realm_user_in_with_mapped_roles_and_an_unmapped_user_without()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertKeycloakSignInAsync("PostgreSql", container.GetConnectionString(), trustServerCertificate: false);
    }



    private static async Task AssertKeycloakSignInAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        await TestMigrations.ApplyAsync(provider, connectionString);
        var runtime = await TestLogins.CreateRuntimeAsync(provider, connectionString);

        await using var keycloak = Keycloak();
        await keycloak.StartAsync();
        var authority = $"http://{keycloak.Hostname}:{keycloak.GetMappedPublicPort(8080)}/realms/{Realm}";

        await using var app = await StartHostAsync(provider, runtime, trustServerCertificate);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", await TestSessions.SignInAsAdministratorAsync(client));

        await ConfigureAsync(app.Services, authority);
        var healthy = await CheckAsync(client);
        var supervisor = (await client.GetFromJsonAsync<List<RoleInfo>>("/api/v0/auth/roles", Json))!.Single(r => string.Equals(r.Name, "Supervisor", StringComparison.Ordinal));
        using var mapped = await client.PostAsync(new Uri("/api/v0/auth/providers/oidc/groups", UriKind.Relative), Body(new GroupRoleMappingDraft("wms-supervisors", supervisor.Id, SiteId: null)));

        var alice = await SignInThroughKeycloakAsync(app, authority, "alice", "alice-password");
        var bob = await SignInThroughKeycloakAsync(app, authority, "bob", "bob-password");
        using var aliceGranted = await SettingsRegistryAsync(app, alice);
        using var bobRefused = await SettingsRegistryAsync(app, bob);
        var aliceMe = await MeAsync(app, alice);
        var bobMe = await MeAsync(app, bob);

        Assert.True(healthy.Healthy, healthy.Detail);
        Assert.Contains("issuer " + authority, healthy.Detail, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Created, mapped.StatusCode);
        Assert.Equal(("alice", "Alice Example", false), (aliceMe.UserName, aliceMe.DisplayName, aliceMe.IsLocalAdmin));
        Assert.Contains("settings.read@organization", aliceMe.Permissions);
        Assert.Equal(HttpStatusCode.OK, aliceGranted.StatusCode);
        Assert.Equal("bob", bobMe.UserName);
        Assert.Empty(bobMe.Permissions);   // E11.2: a user in no mapped group holds no role
        Assert.Equal(HttpStatusCode.Forbidden, bobRefused.StatusCode);
        await AssertAccountsAsync(app.Services);
    }



    /// <summary>
    /// Keycloak in development mode with the test realm imported at start: the realm, the confidential client
    /// with the callback as its redirect URI and a group-membership mapper named <c>groups</c>, one group, and
    /// two users with known passwords. The same realm is the recipe in docs/KEYCLOAK.md.
    /// </summary>
    private static IContainer Keycloak()
    {
        return new ContainerBuilder("quay.io/keycloak/keycloak:26.4")
            .WithCommand("start-dev", "--import-realm")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", "admin-password-for-tests")
            .WithResourceMapping(Encoding.UTF8.GetBytes(RealmExport), "/opt/keycloak/data/import/wms-realm.json")
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath($"/realms/{Realm}/.well-known/openid-configuration")))
            .Build();
    }



    /// <summary>
    /// The realm as Keycloak's export format wants it (the pieces that matter; everything else defaults).
    /// </summary>
    private const string RealmExport = """
        {
          "realm": "wms",
          "enabled": true,
          "sslRequired": "none",
          "clients": [
            {
              "clientId": "wms-console",
              "enabled": true,
              "publicClient": false,
              "secret": "wms-console-secret-for-tests",
              "standardFlowEnabled": true,
              "directAccessGrantsEnabled": false,
              "redirectUris": ["http://localhost/auth/oidc/callback"],
              "webOrigins": ["http://localhost"],
              "attributes": { "pkce.code.challenge.method": "S256" },
              "protocolMappers": [
                {
                  "name": "groups",
                  "protocol": "openid-connect",
                  "protocolMapper": "oidc-group-membership-mapper",
                  "consentRequired": false,
                  "config": {
                    "claim.name": "groups",
                    "full.path": "false",
                    "id.token.claim": "true",
                    "access.token.claim": "true",
                    "userinfo.token.claim": "true"
                  }
                }
              ]
            }
          ],
          "groups": [
            { "name": "wms-supervisors", "path": "/wms-supervisors" },
            { "name": "everyone", "path": "/everyone" }
          ],
          "users": [
            {
              "username": "alice",
              "enabled": true,
              "email": "alice@example.test",
              "emailVerified": true,
              "firstName": "Alice",
              "lastName": "Example",
              "credentials": [{ "type": "password", "value": "alice-password", "temporary": false }],
              "groups": ["/wms-supervisors", "/everyone"]
            },
            {
              "username": "bob",
              "enabled": true,
              "email": "bob@example.test",
              "emailVerified": true,
              "firstName": "Bob",
              "lastName": "Example",
              "credentials": [{ "type": "password", "value": "bob-password", "temporary": false }],
              "groups": ["/everyone"]
            }
          ]
        }
        """;



    private static async Task ConfigureAsync(IServiceProvider services, string authority)
    {
        using var scope = services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettings>();
        var organization = SettingScopeRef.Organization;
        await settings.SetAsync(OidcSettings.Authority, organization, authority, "test", CancellationToken.None);
        await settings.SetAsync(OidcSettings.ClientId, organization, ClientId, "test", CancellationToken.None);
        await settings.SetAsync(OidcSettings.ClientSecret, organization, new SecretText(ClientSecret), "test", CancellationToken.None);
        await settings.SetAsync(OidcSettings.RequireHttps, organization, false, "test", CancellationToken.None);
        await settings.SetAsync(AuthProviderSettings.Enabled, organization, "oidc,local", "test", CancellationToken.None);
        await services.GetRequiredService<AuthProviderState>().RefreshAsync(CancellationToken.None);
    }



    /// <summary>
    /// The browser's side against a real login page: the challenge redirects to Keycloak, whose page carries
    /// the login form; the form is posted with the credentials and Keycloak's own cookies; Keycloak redirects to
    /// the callback with a code; the callback, sent with the correlation and nonce cookies, exchanges it over the
    /// back channel and answers with the session cookie.
    /// </summary>
    private static async Task<string> SignInThroughKeycloakAsync(WebApplication app, string authority, string userName, string password)
    {
        using var browser = app.GetTestClient();
        using var challenge = await browser.GetAsync(new Uri("/api/v0/auth/oidc/challenge?returnUrl=/console", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var authorize = challenge.Headers.Location!;
        Assert.StartsWith(authority + "/protocol/openid-connect/auth", authorize.ToString(), StringComparison.Ordinal);
        var cookies = string.Join("; ", challenge.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));

        // Keycloak marks its cookies Secure even over the lab's plain HTTP, and HttpClient's cookie container
        // would then drop them; a jar of our own keeps the session across the redirects and the login post.
        using var provider = new ProviderBrowser();
        using var page = await FollowProviderRedirectsAsync(provider, await provider.GetAsync(authorize), authority);
        var html = await page.Content.ReadAsStringAsync();
        Assert.True(page.StatusCode == HttpStatusCode.OK, $"Keycloak answered {(int)page.StatusCode} for {page.RequestMessage?.RequestUri}: {Regex.Replace(html, @"<[^>]+>|\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1))[..Math.Min(600, html.Length)]}");
        var action = FormAction.Match(html);
        Assert.True(action.Success, "Keycloak's login page has no kc-form-login form: " + html[..Math.Min(html.Length, 400)]);
        var form = new Uri(WebUtility.HtmlDecode(action.Groups["action"].Value));

        using var posted = await FollowProviderRedirectsAsync(provider, await provider.PostAsync(form, new FormUrlEncodedContent([new("username", userName), new("password", password), new("credentialId", string.Empty)])), authority);
        var body = await posted.Content.ReadAsStringAsync();
        Assert.True(posted.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found, $"the login post answered {(int)posted.StatusCode}: {body[..Math.Min(400, body.Length)]}");
        var callback = posted.Headers.Location!;
        Assert.StartsWith("http://localhost/auth/oidc/callback?", callback.ToString(), StringComparison.Ordinal);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(callback.PathAndQuery, UriKind.Relative));
        request.Headers.Add("Cookie", cookies);
        using var signedIn = await browser.SendAsync(request);
        Assert.True(signedIn.StatusCode == HttpStatusCode.Redirect, $"callback answered {(int)signedIn.StatusCode}: {await signedIn.Content.ReadAsStringAsync()}");
        Assert.Equal("/console", signedIn.Headers.Location!.ToString());
        return signedIn.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("wms.session=", StringComparison.Ordinal)).Split(';')[0];
    }



    /// <summary>
    /// Keycloak answers the authorization request and the login post with redirects inside its own realm
    /// (session set-up, login actions) before the page or the final redirect to the console; follow those,
    /// cookies kept, and stop at the first response that is not a redirect within the provider.
    /// </summary>
    private static async Task<HttpResponseMessage> FollowProviderRedirectsAsync(ProviderBrowser provider, HttpResponseMessage response, string authority)
    {
        for (var hops = 0; hops < 10; hops++)
        {
            if (response.StatusCode is not (HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther) || response.Headers.Location is not { } next || !next.ToString().StartsWith(authority, StringComparison.Ordinal))
            {
                return response;
            }

            response.Dispose();
            response = await provider.GetAsync(next);
        }

        return response;
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



    private static async Task AssertAccountsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var users = await context.Users.Where(u => u.Provider == "oidc").OrderBy(u => u.UserName).ToListAsync();

        Assert.Equal(["alice", "bob"], users.Select(u => u.UserName));
        Assert.All(users, u => Assert.Null(u.PasswordHash));
        Assert.All(users, u => Assert.NotNull(u.Signature));
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
        builder.Services.AddWmsOidcProvider();
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



    /// <summary>
    /// A minimal browser for the provider's side: no automatic redirects, and a cookie jar that keeps every
    /// cookie the provider sets regardless of its Secure flag (the lab talks plain HTTP to Keycloak).
    /// </summary>
    private sealed class ProviderBrowser : IDisposable
    {
        private readonly HttpClient _client = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        private readonly Dictionary<string, string> _jar = new(StringComparer.Ordinal);



        public Task<HttpResponseMessage> GetAsync(Uri uri)
        {
            return SendAsync(new HttpRequestMessage(HttpMethod.Get, uri));
        }



        public Task<HttpResponseMessage> PostAsync(Uri uri, HttpContent content)
        {
            return SendAsync(new HttpRequestMessage(HttpMethod.Post, uri) { Content = content });
        }



        public void Dispose()
        {
            _client.Dispose();
        }



        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            using (request)
            {
                if (_jar.Count > 0)
                {
                    request.Headers.Add("Cookie", string.Join("; ", _jar.Select(c => c.Key + "=" + c.Value)));
                }

                var response = await _client.SendAsync(request);
                if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
                {
                    foreach (var pair in setCookies.Select(c => c.Split(';')[0]).Where(c => c.Contains('=', StringComparison.Ordinal)))
                    {
                        var at = pair.IndexOf('=', StringComparison.Ordinal);
                        _jar[pair[..at].Trim()] = pair[(at + 1)..].Trim();
                    }
                }

                return response;
            }
        }
    }
}
