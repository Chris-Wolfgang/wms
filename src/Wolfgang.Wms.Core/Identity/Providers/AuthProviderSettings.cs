// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// The provider selection (E11.0): an install uses one identity provider, chosen with a setting;
/// <c>appsettings</c> holds one emergency override that forces <c>local</c> only. Under single sign-on the
/// local administrator stays available as the break-glass path, which is not part of this setting.
/// </summary>
public static class AuthProviderSettings
{
    /// <summary>
    /// The bootstrap key that forces local sign-in only, whatever the setting says (a lock-out recovery,
    /// not the normal way to choose providers).
    /// </summary>
    public const string ForceLocalKey = "Wms:Auth:ForceLocal";

    /// <summary>
    /// The local provider's name.
    /// </summary>
    public const string Local = "local";



    /// <summary>
    /// The install's one identity provider.
    /// </summary>
    public static readonly SettingKey<string> Active = new("auth.provider", Local, "The identity provider this install signs in with (local, oidc). One only: an install does not run two at once.")
    {
        Scopes = SettingScopes.Organization,
        Validator = Validate,
    };



    /// <summary>
    /// Null for one well-formed name (registration is checked when the setting is applied, so a provider can
    /// be named before its release arrives), else the reason.
    /// </summary>
    public static string? Validate(string? value)
    {
        return AuthProviderCatalog.IsName(value) ? null : $"'{value}' is not one provider name (lower-case letters, digits and dashes)";
    }
}
