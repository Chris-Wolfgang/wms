// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Identity.Providers;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.AotSmoke;

/// <summary>
/// A challenge provider's <see cref="IChallengeAuthProvider.HandlerType"/> becomes an authentication scheme at run
/// time (<see cref="AuthProviderState.RefreshAsync"/>), and ASP.NET creates the handler from that type through its
/// public constructor. Under NativeAOT the constructor survives only because of the
/// <c>[DynamicallyAccessedMembers(PublicConstructors)]</c> annotation, so the real refresh and the real handler
/// activation run here.
/// </summary>
internal static class AuthProvidersSmoke
{
    private const string ProviderName = "smoke";



    public static async Task ChallengeSchemeFollowsTheSetting()
    {
        var settings = new SmokeSettings();
        await using var services = Host(settings);
        var state = services.GetRequiredService<AuthProviderState>();
        var schemes = services.GetRequiredService<IAuthenticationSchemeProvider>();

        settings.Enabled = $"{ProviderName}, {AuthProviderSettings.Local}";
        await state.RefreshAsync(CancellationToken.None).ConfigureAwait(false);

        Smoke.SequenceEqual([ProviderName, AuthProviderSettings.Local], state.Enabled.Select(p => p.Name));
        var scheme = await schemes.GetSchemeAsync(ProviderName).ConfigureAwait(false)
            ?? throw new SmokeFailureException("the enabled challenge provider has no scheme");
        Smoke.SequenceEqual([typeof(SmokeHandler).FullName!], [scheme.HandlerType.FullName!]);

        settings.Enabled = AuthProviderSettings.Local;
        await state.RefreshAsync(CancellationToken.None).ConfigureAwait(false);

        if (await schemes.GetSchemeAsync(ProviderName).ConfigureAwait(false) is not null)
        {
            throw new SmokeFailureException("the disabled challenge provider's scheme was not removed");
        }
    }



    public static async Task HandlerIsActivatedFromItsType()
    {
        var settings = new SmokeSettings { Enabled = ProviderName };
        await using var services = Host(settings);
        await services.GetRequiredService<AuthProviderState>().RefreshAsync(CancellationToken.None).ConfigureAwait(false);
        var context = new DefaultHttpContext { RequestServices = services };

        var handler = await services
            .GetRequiredService<IAuthenticationHandlerProvider>()
            .GetHandlerAsync(context, ProviderName)
            .ConfigureAwait(false);
        var result = handler is null
            ? throw new SmokeFailureException("no handler was created for the scheme")
            : await handler.AuthenticateAsync().ConfigureAwait(false);

        Smoke.SequenceEqual([typeof(SmokeHandler).FullName!], [handler.GetType().FullName!]);
        Smoke.SequenceEqual([bool.TrueString], [result.None.ToString()]);
    }



    private static ServiceProvider Host(SmokeSettings settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAuthentication();
        services.AddSingleton<IAuthProvider, SmokeChallengeProvider>();
        services.AddSingleton<IAuthProvider, LocalAuthProvider>();
        services.AddSingleton<AuthProviderCatalog>();
        services.AddSingleton<AuthProviderState>();
        services.AddScoped<ISettings>(_ => settings);
        return services.BuildServiceProvider();
    }



    private sealed class SmokeChallengeProvider : IChallengeAuthProvider
    {
        public string Name => ProviderName;

        public string DisplayName => "Smoke";

        public AuthProviderKind Kind => AuthProviderKind.Challenge;

        public IReadOnlyList<SettingKey> Settings => [];

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type HandlerType => typeof(SmokeHandler);

        public Task<AuthProviderHealth> CheckAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AuthProviderHealth(Healthy: true, Detail: "smoke"));
        }

        public ExternalIdentity Identify(ClaimsPrincipal principal)
        {
            return new ExternalIdentity("subject", "user", "User", []);
        }

        public Task ApplyAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }



    private sealed class SmokeHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public SmokeHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }



        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
    }



    /// <summary>
    /// The auth module's defaults, with the enabled-provider list overridable.
    /// </summary>
    private sealed class SmokeSettings : ISettings
    {
        private readonly DefaultSettings _defaults = new(new SettingRegistry(AuthSettings.All));

        public string Enabled { get; set; } = AuthProviderSettings.Local;

        public async Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken)
        {
            return string.Equals(key.Name, AuthProviderSettings.Enabled.Name, StringComparison.Ordinal) && key.Codec.TryParse(Enabled, out var value)
                ? value
                : await _defaults.GetAsync(key, scope, cancellationToken).ConfigureAwait(false);
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
