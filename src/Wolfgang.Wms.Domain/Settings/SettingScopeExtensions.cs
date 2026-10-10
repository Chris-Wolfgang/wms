// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Settings;

/// <summary>
/// Helpers over <see cref="SettingScope"/> and <see cref="SettingScopes"/>.
/// </summary>
public static class SettingScopeExtensions
{
    /// <summary>
    /// The flag for one scope.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scope"/> is not a defined scope.</exception>
    public static SettingScopes AsFlag(this SettingScope scope)
    {
        return scope switch
        {
            SettingScope.Organization => SettingScopes.Organization,
            SettingScope.Site => SettingScopes.Site,
            SettingScope.Zone => SettingScopes.Zone,
            SettingScope.Sku => SettingScopes.Sku,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown setting scope."),
        };
    }



    /// <summary>
    /// The scope a value is inherited from, or null at the organisation. Zones and SKUs both inherit from
    /// their site.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scope"/> is not a defined scope.</exception>
    public static SettingScope? Parent(this SettingScope scope)
    {
        return scope switch
        {
            SettingScope.Organization => null,
            SettingScope.Site => SettingScope.Organization,
            SettingScope.Zone => SettingScope.Site,
            SettingScope.Sku => SettingScope.Site,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown setting scope."),
        };
    }



    /// <summary>
    /// The stored name of a scope (<c>organization</c>, <c>site</c>, <c>zone</c>, <c>sku</c>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scope"/> is not a defined scope.</exception>
    public static string StoredName(this SettingScope scope)
    {
        return scope switch
        {
            SettingScope.Organization => "organization",
            SettingScope.Site => "site",
            SettingScope.Zone => "zone",
            SettingScope.Sku => "sku",
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown setting scope."),
        };
    }



    /// <summary>
    /// Parses a stored scope name (case-insensitive).
    /// </summary>
    public static bool TryParseScope(string? name, out SettingScope scope)
    {
        switch (name?.Trim().ToUpperInvariant())
        {
            case "ORGANIZATION":
                scope = SettingScope.Organization;
                return true;
            case "SITE":
                scope = SettingScope.Site;
                return true;
            case "ZONE":
                scope = SettingScope.Zone;
                return true;
            case "SKU":
                scope = SettingScope.Sku;
                return true;
            default:
                scope = default;
                return false;
        }
    }



    /// <summary>
    /// True when <paramref name="scopes"/> includes <paramref name="scope"/>.
    /// </summary>
    public static bool Allows(this SettingScopes scopes, SettingScope scope)
    {
        return (scopes & scope.AsFlag()) != SettingScopes.None;
    }
}
