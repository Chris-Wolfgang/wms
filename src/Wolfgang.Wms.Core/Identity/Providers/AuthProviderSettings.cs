// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// The provider selection (E11.0): which providers are enabled is a setting, applied without a restart;
/// <c>appsettings</c> holds one emergency override that forces <c>local</c> only.
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
    /// The enabled providers, comma-separated, in the order the login page lists them.
    /// </summary>
    public static readonly SettingKey<string> Enabled = new("auth.providers.enabled", Local, "Identity providers the console offers, comma-separated, in login-page order (local, oidc).")
    {
        Scopes = SettingScopes.Organization,
        Validator = Validate,
    };



    /// <summary>
    /// The names in an <see cref="Enabled"/> value: trimmed, distinct, in order.
    /// </summary>
    public static IReadOnlyList<string> Parse(string? value)
    {
        return (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList();
    }



    /// <summary>
    /// Null for a list of well-formed names (registration is checked when the setting is applied, so a
    /// provider can be named before its release arrives), else the reason.
    /// </summary>
    public static string? Validate(string? value)
    {
        var bad = Parse(value).FirstOrDefault(n => !AuthProviderCatalog.IsName(n));
        return bad is null ? null : $"'{bad}' is not a provider name (lower-case letters, digits and dashes)";
    }
}
