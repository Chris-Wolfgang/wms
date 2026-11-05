// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Auth.Oidc;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Identity.External;
using Wolfgang.Wms.Core.Identity.Providers;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.UnitTests.Identity;

/// <summary>
/// E11.1 without a provider: the settings validate; the snapshot reads them (secret revealed, authority
/// trimmed); the options are built from the snapshot and rebuilt after a change; claims map to the WMS
/// identity with fallbacks; the check refuses an unconfigured provider; the placeholders answer
/// unavailable; the registration resolves.
/// </summary>
public sealed class OidcUnitTests
{
    [Fact]
    public void Settings_validate_their_values()
    {
        Assert.Null(OidcSettings.Authority.Validate(string.Empty));
        Assert.Null(OidcSettings.Authority.Validate("https://login.example.com/realms/wms"));
        Assert.NotNull(OidcSettings.Authority.Validate("login.example.com"));
        Assert.NotNull(OidcSettings.Authority.Validate("ftp://x"));
        Assert.NotNull(OidcSettings.DisplayName.Validate(string.Empty));
        Assert.NotNull(OidcSettings.ClientId.Validate(new string('x', 257)));
        Assert.NotNull(OidcSettings.Scopes.Validate(new string('x', 513)));
        Assert.Null(OidcSettings.GroupClaim.Validate("groups"));
        Assert.NotNull(OidcSettings.GroupClaim.Validate("my groups"));
        Assert.NotNull(OidcSettings.NameClaim.Validate(string.Empty));
        Assert.Equal(SettingKind.Secret, OidcSettings.ClientSecret.Kind);
        Assert.Equal(9, OidcSettings.All.Count);
        Assert.False(OidcSnapshot.Default.IsConfigured);
    }



    [Fact]
    public async Task The_snapshot_reads_the_settings_and_the_options_follow_it()
    {
        var settings = new FakeSettings();
        settings.Values["auth.oidc.authority"] = " https://login.example.com/realms/wms/ ";
        settings.Values["auth.oidc.client_id"] = "wms";
        settings.Values["auth.oidc.client_secret"] = "s3cret";
        settings.Values["auth.oidc.scopes"] = "profile groups";
        settings.Values["auth.oidc.group_claim"] = "roles";
        using var provider = Host(settings);
        var oidc = provider.GetRequiredService<OidcAuthProvider>();
        var monitor = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>();

        var before = monitor.Get("oidc");
        await oidc.ApplyAsync(provider, CancellationToken.None);
        var options = monitor.Get("oidc");

        Assert.Null(before.Authority);
        Assert.Equal("https://login.example.com/realms/wms", options.Authority);
        Assert.Equal(("wms", "s3cret", true), (options.ClientId, options.ClientSecret, options.RequireHttpsMetadata));
        Assert.Equal(["openid", "profile", "groups"], options.Scope);
        Assert.Equal(("roles", "preferred_username"), (options.TokenValidationParameters.RoleClaimType, options.TokenValidationParameters.NameClaimType));
        Assert.Equal(("/auth/oidc/callback", "Cookies", "code", "query", true, false), (options.CallbackPath.Value, options.SignInScheme, options.ResponseType, options.ResponseMode, options.UsePkce, options.MapInboundClaims));
        Assert.True(oidc.Snapshot.IsConfigured);
        Assert.Equal("Single sign-on", oidc.DisplayName);
        Assert.Equal((AuthProviderKind.Challenge, typeof(OpenIdConnectHandler), "oidc"), (oidc.Kind, oidc.HandlerType, oidc.Name));
        Assert.Same(OidcSettings.All, oidc.Settings);
        Assert.DoesNotContain("groups", monitor.Get("other").Scope);   // other names untouched
    }



    [Fact]
    public async Task Claims_map_to_the_identity_with_fallbacks()
    {
        var settings = new FakeSettings();
        using var provider = Host(settings);
        var oidc = provider.GetRequiredService<OidcAuthProvider>();
        await oidc.ApplyAsync(provider, CancellationToken.None);
        var full = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "s1"), new Claim("preferred_username", "alice"), new Claim("name", "Alice"), new Claim("groups", "g1"), new Claim("groups", "g2"), new Claim("groups", "g1"), new Claim("groups", "")]));
        var sparse = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "s2"), new Claim("email", "bob@example.com")]));

        var alice = oidc.Identify(full);
        var bob = oidc.Identify(sparse);
        Assert.Equal(("s1", "alice", "Alice"), (alice.Subject, alice.UserName, alice.DisplayName));
        Assert.Equal(["g1", "g2"], alice.Groups);
        Assert.Equal(("s2", "bob@example.com", "bob@example.com"), (bob.Subject, bob.UserName, bob.DisplayName));
        Assert.Empty(bob.Groups);
        Assert.Equal(string.Empty, oidc.Identify(new ClaimsPrincipal()).Subject);
        Assert.Throws<ArgumentNullException>(() => oidc.Identify(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => oidc.ApplyAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => oidc.CheckAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => OidcSnapshot.ReadAsync(null!, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => new OidcAuthProvider(null!));
        Assert.Throws<ArgumentNullException>(() => new OidcOptionsConfigurator(null!));
        Assert.Throws<ArgumentNullException>(() => new OidcOptionsConfigurator(oidc).Configure(null!));
    }



    [Fact]
    public async Task The_check_refuses_an_unconfigured_provider_and_a_bad_authority()
    {
        var settings = new FakeSettings();
        using var provider = Host(settings);
        var oidc = provider.GetRequiredService<OidcAuthProvider>();

        var unconfigured = await oidc.CheckAsync(provider, CancellationToken.None);
        settings.Values["auth.oidc.authority"] = "https://localhost:1";
        settings.Values["auth.oidc.client_id"] = "wms";
        var unreachable = await oidc.CheckAsync(provider, CancellationToken.None);

        Assert.False(unconfigured.Healthy);
        Assert.Contains("auth.oidc.authority", unconfigured.Detail, StringComparison.Ordinal);
        Assert.False(unreachable.Healthy);
        Assert.StartsWith("Discovery failed", unreachable.Detail, StringComparison.Ordinal);
    }



    [Fact]
    public async Task Placeholders_and_registration()
    {
        var accounts = new NoExternalAccounts();
        var mappings = new NoGroupRoleMappings();
        var identity = new ExternalIdentity("s", "u", "U", []);

        Assert.Empty(await mappings.ListAsync("oidc", CancellationToken.None));
        await Assert.ThrowsAsync<AuthException>(() => accounts.SignInAsync("oidc", identity, CancellationToken.None));
        await Assert.ThrowsAsync<AuthException>(() => mappings.AddAsync("oidc", new GroupRoleMappingDraft("g", 1), "me", CancellationToken.None));
        await Assert.ThrowsAsync<AuthException>(() => mappings.RemoveAsync(1, "me", CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => ExternalSignInResult.Succeeded(null!));
        Assert.Equal(ExternalSignInOutcome.Disabled, ExternalSignInResult.Refused(ExternalSignInOutcome.Disabled).Outcome);
        Assert.Throws<ArgumentNullException>(() => OidcServiceCollectionExtensions.AddWmsOidcProvider(null!));
        var fake = new FakeSettings();
        var key = OidcSettings.Authority;
        var scope = SettingScopeRef.Organization;
        await Assert.ThrowsAsync<NotSupportedException>(() => fake.SetAsync(key, scope, "x", "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => fake.ResetAsync(key, scope, "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => fake.SetModeAsync(key, scope, CascadeMode.Value, "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => fake.PopulateAsync(scope, "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => fake.ListAsync(scope, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => fake.SetTextAsync(key.Name, scope, "x", "me", CancellationToken.None));
        Assert.Equal(string.Empty, (await fake.FindAsync(key.Name, scope, CancellationToken.None)).EffectiveValue);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWmsAuthModule();
        services.AddWmsOidcProvider();
        using var provider = services.BuildServiceProvider();
        Assert.Equal(["local", "oidc"], provider.GetRequiredService<AuthProviderCatalog>().All.Select(p => p.Name));
        Assert.Contains(provider.GetRequiredService<ModuleCollection>().Modules, m => string.Equals(m.Name, "oidc", StringComparison.Ordinal) && m.Settings.Count == 9);
    }



    [Theory]
    [InlineData(ExternalSignInOutcome.Disabled, StatusCodes.Status403Forbidden)]
    [InlineData(ExternalSignInOutcome.IntegrityFailure, StatusCodes.Status403Forbidden)]
    public async Task A_refused_account_answers_a_problem(ExternalSignInOutcome outcome, int status)
    {
        using var provider = EventHost(new FakeAccounts(ExternalSignInResult.Refused(outcome)));
        var context = TokenContext(provider, new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "s1")], "test")));

        await OidcEvents.OnTokenValidatedAsync(context);

        Assert.Equal(status, context.HttpContext.Response.StatusCode);
    }



    [Fact]
    public async Task A_valid_token_becomes_a_session_and_a_token_without_a_subject_is_refused()
    {
        var user = new LocalUser(7, "alice", "Alice", MustChangePassword: false, IsLocalAdmin: false, IsDisabled: false, Grants: ["*@organization"]);
        using var provider = EventHost(new FakeAccounts(ExternalSignInResult.Succeeded(user)));
        var signedIn = TokenContext(provider, new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "s1")], "test")));
        signedIn.Properties = null;
        var noSubject = TokenContext(provider, principal: null);

        await OidcEvents.OnTokenValidatedAsync(signedIn);
        await OidcEvents.OnTokenValidatedAsync(noSubject);

        Assert.Equal("alice", signedIn.Principal!.Identity!.Name);
        Assert.False(signedIn.Properties!.IsPersistent);
        Assert.Equal(StatusCodes.Status502BadGateway, noSubject.HttpContext.Response.StatusCode);
        await Assert.ThrowsAsync<ArgumentNullException>(() => OidcEvents.OnTokenValidatedAsync(null!));
    }



    [Fact]
    public async Task A_provider_failure_answers_a_problem()
    {
        using var provider = EventHost(new FakeAccounts(ExternalSignInResult.Refused(ExternalSignInOutcome.Disabled)));
        var failed = new RemoteFailureContext(EventHttp(provider), Scheme, new OpenIdConnectOptions(), new InvalidOperationException("state mismatch"));
        var unknown = new RemoteFailureContext(EventHttp(provider), Scheme, new OpenIdConnectOptions(), failure: null!);

        await OidcEvents.OnRemoteFailureAsync(failed);
        await OidcEvents.OnRemoteFailureAsync(unknown);

        Assert.Equal(StatusCodes.Status502BadGateway, failed.HttpContext.Response.StatusCode);
        Assert.Equal(StatusCodes.Status502BadGateway, unknown.HttpContext.Response.StatusCode);
        Assert.True(failed.Result!.Handled);
        await Assert.ThrowsAsync<ArgumentNullException>(() => OidcEvents.OnRemoteFailureAsync(null!));
    }



    private static readonly AuthenticationScheme Scheme = new("oidc", "oidc", typeof(OpenIdConnectHandler));



    private static ServiceProvider EventHost(IExternalAccounts accounts)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddHttpClient();
        services.AddDataProtection();
        services.AddSingleton<OidcAuthProvider>();
        services.AddSingleton(accounts);
        services.AddScoped<ISettings>(_ => new FakeSettings());
        return services.BuildServiceProvider();
    }



    private static DefaultHttpContext EventHttp(IServiceProvider provider)
    {
        return new DefaultHttpContext { RequestServices = provider, Response = { Body = new MemoryStream() } };
    }



    private static TokenValidatedContext TokenContext(IServiceProvider provider, ClaimsPrincipal? principal)
    {
        return new TokenValidatedContext(EventHttp(provider), Scheme, new OpenIdConnectOptions(), principal!, new AuthenticationProperties());
    }



    private sealed class FakeAccounts(ExternalSignInResult result) : IExternalAccounts
    {
        public Task<ExternalSignInResult> SignInAsync(string provider, ExternalIdentity identity, CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }



    private static ServiceProvider Host(FakeSettings settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddHttpClient();
        services.AddDataProtection();
        services.AddSingleton<OidcAuthProvider>();
        services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>, OidcOptionsConfigurator>();
        services.AddScoped<ISettings>(_ => settings);
        return services.BuildServiceProvider();
    }



    private sealed class FakeSettings : ISettings
    {
        private readonly DefaultSettings _defaults = new(new SettingRegistry(OidcSettings.All));

        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        public async Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken)
        {
            return Values.TryGetValue(key.Name, out var text) && key.Codec.TryParse(text, out var value) ? value : await _defaults.GetAsync(key, scope, cancellationToken);
        }

        public Task<SettingValue> FindAsync(string name, SettingScopeRef scope, CancellationToken cancellationToken) => _defaults.FindAsync(name, scope, cancellationToken);

        public Task<SettingValue> SetAsync<T>(SettingKey<T> key, SettingScopeRef scope, T value, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingValue> ResetAsync(SettingKey key, SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingValue> SetModeAsync(SettingKey key, SettingScopeRef scope, CascadeMode mode, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> PopulateAsync(SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<SettingValue>> ListAsync(SettingScopeRef scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingValue> SetTextAsync(string name, SettingScopeRef scope, string? text, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
