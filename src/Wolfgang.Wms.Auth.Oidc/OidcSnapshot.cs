// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Auth.Oidc;

/// <summary>
/// The OIDC settings as last read (E11.1): the handler's options are built from this synchronously, and it
/// is replaced whenever <see cref="OidcAuthProvider.ApplyAsync"/> runs (a settings change, or enablement).
/// </summary>
/// <param name="DisplayName">The login-page label.</param>
/// <param name="Authority">The issuer URL, or empty when not configured.</param>
/// <param name="ClientId">The client id.</param>
/// <param name="ClientSecret">The client secret, or empty.</param>
/// <param name="Scopes">The scopes, space-separated.</param>
/// <param name="GroupClaim">The group claim type.</param>
/// <param name="NameClaim">The sign-in name claim type.</param>
/// <param name="DisplayNameClaim">The display name claim type.</param>
/// <param name="RequireHttps">Whether discovery must be HTTPS.</param>
public sealed record OidcSnapshot
(
    string DisplayName,
    string Authority,
    string ClientId,
    string ClientSecret,
    string Scopes,
    string GroupClaim,
    string NameClaim,
    string DisplayNameClaim,
    bool RequireHttps
)
{
    /// <summary>
    /// The defaults, before any read: not configured.
    /// </summary>
    public static OidcSnapshot Default { get; } = new
    (
        OidcSettings.DisplayName.DefaultValue,
        OidcSettings.Authority.DefaultValue,
        OidcSettings.ClientId.DefaultValue,
        string.Empty,
        OidcSettings.Scopes.DefaultValue,
        OidcSettings.GroupClaim.DefaultValue,
        OidcSettings.NameClaim.DefaultValue,
        OidcSettings.DisplayNameClaim.DefaultValue,
        OidcSettings.RequireHttps.DefaultValue
    );



    /// <summary>
    /// True when an authority and a client id are set.
    /// </summary>
    public bool IsConfigured => Authority.Length > 0 && ClientId.Length > 0;



    /// <summary>
    /// Reads every setting at the organisation scope.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    public static async Task<OidcSnapshot> ReadAsync(ISettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var scope = SettingScopeRef.Organization;
        return new OidcSnapshot
        (
            await settings.GetAsync(OidcSettings.DisplayName, scope, cancellationToken).ConfigureAwait(false),
            (await settings.GetAsync(OidcSettings.Authority, scope, cancellationToken).ConfigureAwait(false)).Trim().TrimEnd('/'),
            (await settings.GetAsync(OidcSettings.ClientId, scope, cancellationToken).ConfigureAwait(false)).Trim(),
            (await settings.GetAsync(OidcSettings.ClientSecret, scope, cancellationToken).ConfigureAwait(false)).Value ?? string.Empty,
            await settings.GetAsync(OidcSettings.Scopes, scope, cancellationToken).ConfigureAwait(false),
            await settings.GetAsync(OidcSettings.GroupClaim, scope, cancellationToken).ConfigureAwait(false),
            await settings.GetAsync(OidcSettings.NameClaim, scope, cancellationToken).ConfigureAwait(false),
            await settings.GetAsync(OidcSettings.DisplayNameClaim, scope, cancellationToken).ConfigureAwait(false),
            await settings.GetAsync(OidcSettings.RequireHttps, scope, cancellationToken).ConfigureAwait(false)
        );
    }
}
