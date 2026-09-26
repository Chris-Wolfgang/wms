// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// The providers currently enabled (E11.0), kept in step with the <c>auth.providers.enabled</c> setting
/// by <see cref="AuthProviderSync"/>: a challenge provider's scheme is added to
/// <see cref="IAuthenticationSchemeProvider"/> when it becomes enabled and removed when it is disabled;
/// a provider whose settings changed is told to drop its cached options. Unknown names are logged and
/// skipped. With <see cref="AuthProviderSettings.ForceLocalKey"/> set, only <c>local</c> is enabled.
/// </summary>
public sealed partial class AuthProviderState : IDisposable
{
    private readonly AuthProviderCatalog _catalog;
    private readonly IAuthenticationSchemeProvider _schemes;
    private readonly IServiceScopeFactory _scopes;
    private readonly bool _forceLocal;
    private readonly ILogger<AuthProviderState> _logger;
    private readonly Dictionary<string, string> _fingerprints = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<AuthProviderInfo> _enabled = [];



    /// <summary>
    /// Creates the state.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public AuthProviderState(AuthProviderCatalog catalog, IAuthenticationSchemeProvider schemes, IServiceScopeFactory scopes, IConfiguration configuration, ILogger<AuthProviderState> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _schemes = schemes ?? throw new ArgumentNullException(nameof(schemes));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _forceLocal = bool.TryParse(configuration[AuthProviderSettings.ForceLocalKey], out var force) && force;
    }



    /// <summary>
    /// The enabled providers in login-page order, as of the last refresh.
    /// </summary>
    public IReadOnlyList<AuthProviderInfo> Enabled => Volatile.Read(ref _enabled);



    /// <summary>
    /// True when the bootstrap override forces local sign-in only.
    /// </summary>
    public bool ForceLocal => _forceLocal;



    /// <summary>
    /// The enabled challenge provider of that name, or null.
    /// </summary>
    public IChallengeAuthProvider? FindEnabledChallenge(string? name)
    {
        return Enabled.Any(p => string.Equals(p.Name, name, StringComparison.Ordinal)) ? _catalog.Find(name) as IChallengeAuthProvider : null;
    }



    /// <summary>
    /// Reads the setting and applies it: schemes added or removed, changed providers told to re-read their
    /// settings, the enabled list replaced.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var scope = _scopes.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<ISettings>();
            var names = _forceLocal
                ? [AuthProviderSettings.Local]
                : AuthProviderSettings.Parse(await settings.GetAsync(AuthProviderSettings.Enabled, SettingScopeRef.Organization, cancellationToken).ConfigureAwait(false));
            var enabled = new List<AuthProviderInfo>();
            foreach (var name in names)
            {
                var provider = _catalog.Find(name);
                if (provider is null)
                {
                    LogUnknown(_logger, name);
                    continue;
                }

                if (provider is IChallengeAuthProvider challenge)
                {
                    await EnableChallengeAsync(challenge, settings, scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
                }

                enabled.Add(new AuthProviderInfo(provider.Name, provider.DisplayName, provider.Kind, provider is IChallengeAuthProvider ? ChallengeRoute(provider.Name) : null));
            }

            foreach (var provider in _catalog.All.OfType<IChallengeAuthProvider>().Where(p => !enabled.Any(e => string.Equals(e.Name, p.Name, StringComparison.Ordinal))))
            {
                if (await _schemes.GetSchemeAsync(provider.Name).ConfigureAwait(false) is not null)
                {
                    _schemes.RemoveScheme(provider.Name);
                    _fingerprints.Remove(provider.Name);
                    LogDisabled(_logger, provider.Name);
                }
            }

            Volatile.Write(ref _enabled, enabled);
        }
        finally
        {
            _gate.Release();
        }
    }



    /// <inheritdoc/>
    public void Dispose()
    {
        _gate.Dispose();
    }



    /// <summary>
    /// The API route (relative to the version prefix) that starts a challenge provider's sign-in.
    /// </summary>
    public static string ChallengeRoute(string name)
    {
        return "/auth/" + name + "/challenge";
    }



    private async Task EnableChallengeAsync(IChallengeAuthProvider provider, ISettings settings, IServiceProvider services, CancellationToken cancellationToken)
    {
        var fingerprint = await FingerprintAsync(provider, settings, cancellationToken).ConfigureAwait(false);
        var known = _fingerprints.TryGetValue(provider.Name, out var previous);
        if (!known || !string.Equals(previous, fingerprint, StringComparison.Ordinal))
        {
            await provider.ApplyAsync(services, cancellationToken).ConfigureAwait(false);
            _fingerprints[provider.Name] = fingerprint;
        }

        if (await _schemes.GetSchemeAsync(provider.Name).ConfigureAwait(false) is null)
        {
            _schemes.AddScheme(new AuthenticationScheme(provider.Name, provider.DisplayName, provider.HandlerType));
            LogEnabled(_logger, provider.Name);
        }
    }



    private static async Task<string> FingerprintAsync(IAuthProvider provider, ISettings settings, CancellationToken cancellationToken)
    {
        var parts = new List<string>(provider.Settings.Count);
        foreach (var key in provider.Settings)
        {
            var value = await settings.FindAsync(key.Name, SettingScopeRef.Organization, cancellationToken).ConfigureAwait(false);
            parts.Add(key.Name + "=" + value.EffectiveValue);
        }

        return string.Join('\n', parts);
    }



    [LoggerMessage(Level = LogLevel.Warning, Message = "auth.providers.enabled names '{Name}', which is not a registered provider; skipped.")]
    private static partial void LogUnknown(ILogger logger, string name);



    [LoggerMessage(Level = LogLevel.Information, Message = "Identity provider '{Name}' enabled.")]
    private static partial void LogEnabled(ILogger logger, string name);



    [LoggerMessage(Level = LogLevel.Information, Message = "Identity provider '{Name}' disabled.")]
    private static partial void LogDisabled(ILogger logger, string name);
}
