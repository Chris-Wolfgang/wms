// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Identity.Providers;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E11.0 through the API without a database: the login page's provider list holds <c>local</c> only by
/// default; a challenge for a provider that is not enabled is a 404 problem; the health check needs the
/// manage permission and reports the missing database; a challenge provider registered in the host is
/// offered, challenged and withdrawn as the setting changes, without a restart.
/// </summary>
public sealed class AuthProvidersEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private readonly WebApplicationFactory<Program> _factory;



    public AuthProvidersEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task The_default_host_offers_local_only_and_refuses_unknown_challenges()
    {
        using var client = _factory.WithTestAuth().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var providers = await client.GetFromJsonAsync<List<AuthProviderInfo>>("/api/v0/auth/providers", Json);
        using var unknown = await client.GetAsync(new Uri("/api/v0/auth/nope/challenge", UriKind.Relative));
        using var local = await client.GetAsync(new Uri("/api/v0/auth/local/challenge", UriKind.Relative));
        using var anonymousCheck = await client.PostAsync(new Uri("/api/v0/auth/providers/local/check", UriKind.Relative), content: null);
        using var check = await client.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/auth/providers/local/check", "auth.providers.manage@organization"));
        using var checkUnknown = await client.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/auth/providers/nope/check", "*@organization"));

        Assert.Equal([("local", "Local account", AuthProviderKind.Credentials, null)], providers!.Select(p => (p.Name, p.DisplayName, p.Kind, p.ChallengeUrl)));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("auth.provider_not_enabled", await CodeAsync(unknown));
        Assert.Equal(HttpStatusCode.NotFound, local.StatusCode);   // a credentials provider has no challenge
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCheck.StatusCode);
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        Assert.False((await check.Content.ReadFromJsonAsync<AuthProviderHealth>(Json))!.Healthy);
        Assert.Equal(HttpStatusCode.NotFound, checkUnknown.StatusCode);
    }



    [Fact]
    public async Task A_challenge_provider_is_offered_challenged_and_withdrawn_as_the_setting_changes()
    {
        var settings = new SwitchableSettings();
        using var factory = _factory.WithTestAuth().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IAuthProvider, TeapotProvider>();
            services.RemoveAll<ISettings>();
            services.AddScoped<ISettings>(_ => settings);
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var state = factory.Services.GetRequiredService<AuthProviderState>();

        settings.Enabled = "teapot,local";
        await state.RefreshAsync(CancellationToken.None);
        var offered = await client.GetFromJsonAsync<List<AuthProviderInfo>>("/api/v0/auth/providers", Json);
        using var challenge = await client.GetAsync(new Uri("/api/v0/auth/teapot/challenge?returnUrl=/console", UriKind.Relative));
        using var offsite = await client.GetAsync(new Uri("/api/v0/auth/teapot/challenge?returnUrl=//evil.example", UriKind.Relative));
        using var check = await client.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/auth/providers/teapot/check", "auth.providers.manage@organization"));
        using var scope = factory.Services.CreateScope();
        var authenticated = await scope.ServiceProvider.GetRequiredService<IAuthenticationService>().AuthenticateAsync(new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = scope.ServiceProvider }, "teapot");

        settings.Enabled = "local";
        await state.RefreshAsync(CancellationToken.None);
        var withdrawn = await client.GetFromJsonAsync<List<AuthProviderInfo>>("/api/v0/auth/providers", Json);
        using var gone = await client.GetAsync(new Uri("/api/v0/auth/teapot/challenge", UriKind.Relative));

        Assert.Equal(["teapot", "local"], offered!.Select(p => p.Name));
        Assert.Equal("/auth/teapot/challenge", offered![0].ChallengeUrl);
        Assert.Equal((HttpStatusCode)418, challenge.StatusCode);
        Assert.Equal("/console", challenge.Headers.GetValues("X-Redirect").Single());
        Assert.Equal("/", offsite.Headers.GetValues("X-Redirect").Single());   // only local return URLs
        Assert.Equal(["local"], withdrawn!.Select(p => p.Name));
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal("short and stout", (await check.Content.ReadFromJsonAsync<AuthProviderHealth>(Json))!.Detail);
        Assert.True(authenticated.None);
        Assert.Equal("teapot", new TeapotProvider().Identify(new ClaimsPrincipal()).UserName);
        await AssertReadOnlyAsync(settings);
    }



    private static async Task AssertReadOnlyAsync(ISettings settings)
    {
        var key = AuthProviderSettings.Enabled;
        var scope = SettingScopeRef.Organization;
        Assert.Equal(string.Empty, (await settings.FindAsync(key.Name, scope, CancellationToken.None)).EffectiveValue);
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.SetAsync(key, scope, "x", "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.ResetAsync(key, scope, "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.SetModeAsync(key, scope, CascadeMode.Value, "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.PopulateAsync(scope, "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.ListAsync(scope, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.SetTextAsync(key.Name, scope, "x", "me", CancellationToken.None));
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }



    private sealed class TeapotProvider : IChallengeAuthProvider
    {
        public string Name => "teapot";

        public string DisplayName => "Teapot";

        public AuthProviderKind Kind => AuthProviderKind.Challenge;

        public IReadOnlyList<SettingKey> Settings { get; } = [];

        public Type HandlerType => typeof(TeapotHandler);

        public Task<AuthProviderHealth> CheckAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AuthProviderHealth(Healthy: true, Detail: "short and stout"));
        }

        public ExternalIdentity Identify(ClaimsPrincipal principal)
        {
            return new ExternalIdentity("sub", "teapot", "Teapot", []);
        }

        public Task ApplyAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }



    private sealed class TeapotHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TeapotHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 418;
            Response.Headers["X-Redirect"] = properties.RedirectUri;
            return Task.CompletedTask;
        }
    }



    private sealed class SwitchableSettings : ISettings
    {
        public string Enabled { get; set; } = "local";

        public Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken)
        {
            var text = string.Equals(key.Name, AuthProviderSettings.Enabled.Name, StringComparison.Ordinal) ? Enabled : key.Codec.Format(key.DefaultValue);
            return Task.FromResult(key.Codec.TryParse(text, out var value) ? value : key.DefaultValue);
        }

        public Task<SettingValue> FindAsync(string name, SettingScopeRef scope, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SettingValue(name, "organization", SettingKind.String, null, string.Empty, null, "value", ["value"], null, null, null));
        }

        public Task<SettingValue> SetAsync<T>(SettingKey<T> key, SettingScopeRef scope, T value, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingValue> ResetAsync(SettingKey key, SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingValue> SetModeAsync(SettingKey key, SettingScopeRef scope, CascadeMode mode, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> PopulateAsync(SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<SettingValue>> ListAsync(SettingScopeRef scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingValue> SetTextAsync(string name, SettingScopeRef scope, string? text, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
