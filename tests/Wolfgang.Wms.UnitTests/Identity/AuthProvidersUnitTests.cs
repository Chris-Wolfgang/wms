// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Identity.Providers;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.UnitTests.Identity;

/// <summary>
/// E11.0 without a host: the setting parses and validates names; the catalog refuses duplicates and bad
/// names; the state adds a challenge provider's scheme when the setting names it, tells it to re-read its
/// settings when they change, removes the scheme when it is dropped, skips unknown names, and honours the
/// emergency override; the local provider reports the database; the sync's guards hold.
/// </summary>
public sealed class AuthProvidersUnitTests
{
    [Theory]
    [InlineData(null, "", null)]
    [InlineData("local", "local", null)]
    [InlineData(" local , oidc ,local", "local,oidc", null)]
    [InlineData("local,Oidc", "local,Oidc", "'Oidc' is not a provider name (lower-case letters, digits and dashes)")]
    [InlineData("local,my provider", "local,my provider", "'my provider' is not a provider name (lower-case letters, digits and dashes)")]
    public void The_enabled_setting_parses_and_validates_names(string? value, string expectedNames, string? expectedError)
    {
        Assert.Equal(expectedNames, string.Join(',', AuthProviderSettings.Parse(value)));
        Assert.Equal(expectedError, AuthProviderSettings.Validate(value));
        Assert.Equal(expectedError, AuthProviderSettings.Enabled.Validate(value ?? string.Empty));
    }



    [Fact]
    public void The_catalog_refuses_duplicates_and_bad_names()
    {
        var catalog = new AuthProviderCatalog([new FakeChallengeProvider("fake"), new LocalAuthProvider()]);

        Assert.Equal(["fake", "local"], catalog.All.Select(p => p.Name));
        Assert.IsType<LocalAuthProvider>(catalog.Find("local"));
        Assert.Null(catalog.Find("nope"));
        Assert.Null(catalog.Find(null));
        Assert.Throws<ArgumentNullException>(() => new AuthProviderCatalog(null!));
        Assert.Throws<InvalidOperationException>(() => new AuthProviderCatalog([new LocalAuthProvider(), new LocalAuthProvider()]));
        Assert.Throws<InvalidOperationException>(() => new AuthProviderCatalog([new FakeChallengeProvider("Bad Name")]));
        Assert.Equal("/auth/fake/challenge", AuthProviderState.ChallengeRoute("fake"));
    }



    [Fact]
    public async Task The_state_adds_removes_and_reapplies_challenge_providers_from_the_setting()
    {
        var fake = new FakeChallengeProvider("fake");
        var settings = new FakeSettings();
        using var provider = Host(settings, fake, forceLocal: false);
        var state = provider.GetRequiredService<AuthProviderState>();
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        await state.RefreshAsync(CancellationToken.None);
        Assert.Equal(["local"], state.Enabled.Select(p => p.Name));
        Assert.Null(await schemes.GetSchemeAsync("fake"));
        Assert.Null(state.FindEnabledChallenge("fake"));
        Assert.Null(state.FindEnabledChallenge("local"));

        settings.Values["auth.providers.enabled"] = "fake, local, ghost";
        await state.RefreshAsync(CancellationToken.None);
        Assert.Equal([("fake", AuthProviderKind.Challenge, "/auth/fake/challenge"), ("local", AuthProviderKind.Credentials, null)], state.Enabled.Select(p => (p.Name, p.Kind, p.ChallengeUrl)));
        Assert.Equal(typeof(FakeHandler), (await schemes.GetSchemeAsync("fake"))!.HandlerType);
        Assert.Same(fake, state.FindEnabledChallenge("fake"));
        Assert.Equal(1, fake.Applied);

        await state.RefreshAsync(CancellationToken.None);
        Assert.Equal(1, fake.Applied);   // unchanged settings: not re-applied

        settings.Values["auth.fake.authority"] = "https://issuer.example";
        await state.RefreshAsync(CancellationToken.None);
        Assert.Equal(2, fake.Applied);   // its setting changed: re-applied, scheme kept

        settings.Values["auth.providers.enabled"] = "local";
        await state.RefreshAsync(CancellationToken.None);
        Assert.Equal(["local"], state.Enabled.Select(p => p.Name));
        Assert.Null(await schemes.GetSchemeAsync("fake"));

        settings.Values["auth.providers.enabled"] = "fake";
        await state.RefreshAsync(CancellationToken.None);
        Assert.Equal(3, fake.Applied);   // re-enabled: applied again
        Assert.NotNull(await schemes.GetSchemeAsync("fake"));

        using var scope = provider.CreateScope();
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var result = await scope.ServiceProvider.GetRequiredService<IAuthenticationService>().AuthenticateAsync(http, "fake");
        Assert.True(result.None);   // the dynamically added scheme resolves to the provider's handler
        Assert.True((await fake.CheckAsync(provider, CancellationToken.None)).Healthy);
        Assert.Equal("sub", fake.Identify(new ClaimsPrincipal()).Subject);
    }



    [Fact]
    public async Task The_fake_settings_support_reads_only()
    {
        var settings = new FakeSettings();
        var key = AuthProviderSettings.Enabled;
        var scope = SettingScopeRef.Organization;

        await Assert.ThrowsAsync<NotSupportedException>(() => settings.SetAsync(key, scope, "x", "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.ResetAsync(key, scope, "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.SetModeAsync(key, scope, CascadeMode.Value, "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.PopulateAsync(scope, "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.ListAsync(scope, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.SetTextAsync(key.Name, scope, "x", "me", CancellationToken.None));
    }



    [Fact]
    public async Task The_emergency_override_forces_local_only()
    {
        var fake = new FakeChallengeProvider("fake");
        var settings = new FakeSettings();
        settings.Values["auth.providers.enabled"] = "fake";
        using var provider = Host(settings, fake, forceLocal: true);
        var state = provider.GetRequiredService<AuthProviderState>();

        await state.RefreshAsync(CancellationToken.None);

        Assert.True(state.ForceLocal);
        Assert.Equal(["local"], state.Enabled.Select(p => p.Name));
        Assert.Equal(0, fake.Applied);
        Assert.Null(await provider.GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync("fake"));
    }



    [Fact]
    public async Task The_local_provider_reports_the_database_and_the_guards_hold()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ILocalAccounts, NoLocalAccounts>();
        using var provider = services.BuildServiceProvider();
        var local = new LocalAuthProvider();

        var health = await local.CheckAsync(provider, CancellationToken.None);

        Assert.False(health.Healthy);
        Assert.Contains("not configured", health.Detail, StringComparison.Ordinal);
        Assert.Equal(("local", "Local account", AuthProviderKind.Credentials), (local.Name, local.DisplayName, local.Kind));
        Assert.Equal(["auth.local.lockout_threshold", "auth.local.lockout_duration"], local.Settings.Select(s => s.Name));
        await Assert.ThrowsAsync<ArgumentNullException>(() => local.CheckAsync(null!, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => new AuthProviderSync(null!, TimeProvider.System, NullLogger<AuthProviderSync>.Instance));
        Assert.Throws<ArgumentNullException>(() => new AuthProviderState(null!, null!, null!, null!, null!));
        Assert.Equal(TimeSpan.FromSeconds(5), AuthProviderSync.Interval);
    }



    [Fact]
    public async Task The_sync_refreshes_at_start_and_survives_a_failing_refresh()
    {
        var fake = new FakeChallengeProvider("fake");
        var settings = new FakeSettings();
        settings.Values["auth.providers.enabled"] = "fake";
        using var provider = Host(settings, fake, forceLocal: false);
        var state = provider.GetRequiredService<AuthProviderState>();
        using var failing = new AuthProviderSync(state, TimeProvider.System, NullLogger<AuthProviderSync>.Instance);
        using var sync = new AuthProviderSync(state, TimeProvider.System, NullLogger<AuthProviderSync>.Instance);

        settings.Throw = true;
        await failing.StartAsync(CancellationToken.None);   // logged, nothing enabled yet
        await failing.StopAsync(CancellationToken.None);
        Assert.Empty(state.Enabled);

        settings.Throw = false;
        await sync.StartAsync(CancellationToken.None);
        await sync.StopAsync(CancellationToken.None);
        Assert.Equal(["fake"], state.Enabled.Select(p => p.Name));
    }



    private static ServiceProvider Host(FakeSettings settings, IAuthProvider extra, bool forceLocal)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Wms:Auth:ForceLocal"] = forceLocal ? "true" : null }).Build());
        services.AddAuthentication();
        services.AddSingleton<IAuthProvider>(extra);
        services.AddSingleton<IAuthProvider, LocalAuthProvider>();
        services.AddSingleton<AuthProviderCatalog>();
        services.AddSingleton<AuthProviderState>();
        services.AddScoped<ISettings>(_ => settings);
        return services.BuildServiceProvider();
    }



    private sealed class FakeChallengeProvider : IChallengeAuthProvider
    {
        private static readonly SettingKey<string> Authority = new("auth.fake.authority", string.Empty, "The fake authority.") { Scopes = SettingScopes.Organization };

        public FakeChallengeProvider(string name)
        {
            Name = name;
        }

        public int Applied { get; private set; }

        public string Name { get; }

        public string DisplayName => "Fake";

        public AuthProviderKind Kind => AuthProviderKind.Challenge;

        public IReadOnlyList<SettingKey> Settings { get; } = [Authority];

        public Type HandlerType => typeof(FakeHandler);

        public Task<AuthProviderHealth> CheckAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AuthProviderHealth(Healthy: true, Detail: "fake"));
        }

        public ExternalIdentity Identify(ClaimsPrincipal principal)
        {
            return new ExternalIdentity("sub", "user", "User", []);
        }

        public Task ApplyAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            Applied++;
            return Task.CompletedTask;
        }
    }



    private sealed class FakeHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public FakeHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
    }



    /// <summary>
    /// Defaults from the auth module's registry, overridable per name; the enabled list and a provider's
    /// settings are read through <see cref="GetAsync{T}"/> and <see cref="FindAsync"/> only.
    /// </summary>
    private sealed class FakeSettings : ISettings
    {
        private readonly DefaultSettings _defaults = new(new SettingRegistry(AuthSettings.All.Concat([new SettingKey<string>("auth.fake.authority", string.Empty, "The fake authority.") { Scopes = SettingScopes.Organization }])));

        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        public bool Throw { get; set; }

        public async Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken)
        {
            if (Throw)
            {
                throw new InvalidOperationException("settings unavailable");
            }

            return Values.TryGetValue(key.Name, out var text) && key.Codec.TryParse(text, out var value) ? value : await _defaults.GetAsync(key, scope, cancellationToken);
        }

        public async Task<SettingValue> FindAsync(string name, SettingScopeRef scope, CancellationToken cancellationToken)
        {
            var value = await _defaults.FindAsync(name, scope, cancellationToken);
            return Values.TryGetValue(name, out var text) ? value with { EffectiveValue = text } : value;
        }

        public Task<SettingValue> SetAsync<T>(SettingKey<T> key, SettingScopeRef scope, T value, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingValue> ResetAsync(SettingKey key, SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingValue> SetModeAsync(SettingKey key, SettingScopeRef scope, CascadeMode mode, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> PopulateAsync(SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<SettingValue>> ListAsync(SettingScopeRef scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingValue> SetTextAsync(string name, SettingScopeRef scope, string? text, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
