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
/// E11.3 claim edge cases against <c>mock-oauth2-server</c>, one token shape per client id: a token with no
/// group claim signs the user in with no permissions; a provider that names the claims differently
/// (<c>roles</c>, <c>upn</c>, <c>display</c>) works once the three claim settings point at them; an expired
/// token is refused with <c>502 auth.provider_failed</c>.
/// </summary>
public sealed class OidcClaimEdgeCaseTests
{
    private const string Issuer = "wms";
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;



    [DockerFact]
    public async Task Missing_groups_alternate_claim_names_and_an_expired_token_are_handled()
    {
        await using var database = new PostgreSqlBuilder("postgres:16").Build();
        await using var provider = MockProvider();
        await Task.WhenAll(database.StartAsync(), provider.StartAsync());
        var authority = $"http://{provider.Hostname}:{provider.GetMappedPublicPort(8080)}/{Issuer}";

        await using var app = await StartHostAsync(database.GetConnectionString());
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", await TestSessions.SignInAsAdministratorAsync(client));
        var supervisor = (await client.GetFromJsonAsync<List<RoleInfo>>("/api/v0/auth/roles", Json))!.Single(r => string.Equals(r.Name, "Supervisor", StringComparison.Ordinal));
        using var mapped = await client.PostAsync(new Uri("/api/v0/auth/providers/oidc/groups", UriKind.Relative), Body(new GroupRoleMappingDraft("wms-supervisors", supervisor.Id, SiteId: null)));
        Assert.Equal(HttpStatusCode.Created, mapped.StatusCode);

        await ConfigureAsync(app.Services, authority, "wms-nogroups", groupClaim: "groups", nameClaim: "preferred_username", displayNameClaim: "name");
        var (noGroupsSession, _) = await SignInThroughProviderAsync(app, authority);
        var noGroups = await MeAsync(app, noGroupsSession);

        await ConfigureAsync(app.Services, authority, "wms-altclaims", groupClaim: "roles", nameClaim: "upn", displayNameClaim: "display");
        var (altSession, _) = await SignInThroughProviderAsync(app, authority);
        var alt = await MeAsync(app, altSession);

        await ConfigureAsync(app.Services, authority, "wms-expired", groupClaim: "groups", nameClaim: "preferred_username", displayNameClaim: "name");
        using var expired = await CallbackResponseAsync(app, authority);

        Assert.Equal(("carol", "Carol Example"), (noGroups.UserName, noGroups.DisplayName));
        Assert.Empty(noGroups.Permissions);   // no group claim: signed in, no role
        Assert.Equal(("dave@example.test", "Dave Example"), (alt.UserName, alt.DisplayName));
        Assert.Contains("settings.read@organization", alt.Permissions);   // roles → Supervisor through the mapping
        Assert.Equal(HttpStatusCode.BadGateway, expired.StatusCode);
        Assert.Equal("auth.provider_failed", await CodeAsync(expired));
    }



    /// <summary>
    /// One issuer, three token shapes selected by the client id the host sends.
    /// </summary>
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
                      "match": "wms-nogroups",
                      "claims": { "sub": "carol-sub-1", "preferred_username": "carol", "name": "Carol Example" }
                    },
                    {
                      "requestParam": "client_id",
                      "match": "wms-altclaims",
                      "claims": { "sub": "dave-sub-1", "upn": "dave@example.test", "display": "Dave Example", "roles": ["wms-supervisors"] }
                    }
                  ]
                },
                {
                  "issuerId": "wms",
                  "tokenExpiry": -900,
                  "requestMappings": [
                    {
                      "requestParam": "client_id",
                      "match": "wms-expired",
                      "claims": { "sub": "erin-sub-1", "preferred_username": "erin", "name": "Erin Example", "groups": ["wms-supervisors"] }
                    }
                  ]
                }
              ]
            }
            """;
        return new ContainerBuilder("ghcr.io/navikt/mock-oauth2-server:2.1.10")
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithEnvironment("JSON_CONFIG", config)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath($"/{Issuer}/.well-known/openid-configuration")))
            .Build();
    }



    private static async Task ConfigureAsync(IServiceProvider services, string authority, string clientId, string groupClaim, string nameClaim, string displayNameClaim)
    {
        using var scope = services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettings>();
        var organization = SettingScopeRef.Organization;
        await settings.SetAsync(OidcSettings.Authority, organization, authority, "test", CancellationToken.None);
        await settings.SetAsync(OidcSettings.ClientId, organization, clientId, "test", CancellationToken.None);
        await settings.SetAsync(OidcSettings.ClientSecret, organization, new SecretText("not-checked-by-the-mock"), "test", CancellationToken.None);
        await settings.SetAsync(OidcSettings.GroupClaim, organization, groupClaim, "test", CancellationToken.None);
        await settings.SetAsync(OidcSettings.NameClaim, organization, nameClaim, "test", CancellationToken.None);
        await settings.SetAsync(OidcSettings.DisplayNameClaim, organization, displayNameClaim, "test", CancellationToken.None);
        await settings.SetAsync(OidcSettings.RequireHttps, organization, false, "test", CancellationToken.None);
        await settings.SetAsync(AuthProviderSettings.Enabled, organization, "oidc,local", "test", CancellationToken.None);
        await services.GetRequiredService<AuthProviderState>().RefreshAsync(CancellationToken.None);
    }



    private static async Task<(string Session, string Redirect)> SignInThroughProviderAsync(WebApplication app, string authority)
    {
        using var signedIn = await CallbackResponseAsync(app, authority);
        Assert.True(signedIn.StatusCode == HttpStatusCode.Redirect, $"callback answered {(int)signedIn.StatusCode}: {await signedIn.Content.ReadAsStringAsync()}");
        var session = signedIn.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("wms.session=", StringComparison.Ordinal)).Split(';')[0];
        return (session, signedIn.Headers.Location!.ToString());
    }



    /// <summary>
    /// The browser's side of the flow up to the callback's answer: the challenge, the provider's redirect (no
    /// interactive login on the mock) and the callback sent with the correlation and nonce cookies.
    /// </summary>
    private static async Task<HttpResponseMessage> CallbackResponseAsync(WebApplication app, string authority)
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

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(callback.PathAndQuery, UriKind.Relative));
        request.Headers.Add("Cookie", cookies);
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
