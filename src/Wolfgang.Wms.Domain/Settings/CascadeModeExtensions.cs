// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Settings;

/// <summary>
/// Helpers over <see cref="CascadeMode"/> (E7.2): stored names, the scope a mode delegates to, and the
/// modes a key allows at a scope.
/// </summary>
public static class CascadeModeExtensions
{
    /// <summary>
    /// The stored name of a mode: <c>value</c>, <c>per_site</c>, <c>per_zone</c>, <c>per_sku</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined mode.</exception>
    public static string StoredName(this CascadeMode mode)
    {
        return mode switch
        {
            CascadeMode.Value => "value",
            CascadeMode.PerSite => "per_site",
            CascadeMode.PerZone => "per_zone",
            CascadeMode.PerSku => "per_sku",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown cascade mode."),
        };
    }



    /// <summary>
    /// Parses a stored mode name (case-insensitive); null or empty is <see cref="CascadeMode.Value"/>.
    /// </summary>
    public static bool TryParseMode(string? name, out CascadeMode mode)
    {
        switch (name?.Trim().ToUpperInvariant())
        {
            case null:
            case "":
            case "VALUE":
                mode = CascadeMode.Value;
                return true;
            case "PER_SITE":
                mode = CascadeMode.PerSite;
                return true;
            case "PER_ZONE":
                mode = CascadeMode.PerZone;
                return true;
            case "PER_SKU":
                mode = CascadeMode.PerSku;
                return true;
            default:
                mode = default;
                return false;
        }
    }



    /// <summary>
    /// The scope type a delegating mode hands the decision to, or null for <see cref="CascadeMode.Value"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined mode.</exception>
    public static SettingScope? DelegatesTo(this CascadeMode mode)
    {
        return mode switch
        {
            CascadeMode.Value => null,
            CascadeMode.PerSite => SettingScope.Site,
            CascadeMode.PerZone => SettingScope.Zone,
            CascadeMode.PerSku => SettingScope.Sku,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown cascade mode."),
        };
    }



    /// <summary>
    /// The modes a key with <paramref name="keyScopes"/> allows at <paramref name="scope"/>: <c>value</c>
    /// always, plus delegation to each scope type below <paramref name="scope"/> that the key allows.
    /// </summary>
    public static IReadOnlyList<CascadeMode> AllowedAt(SettingScope scope, SettingScopes keyScopes)
    {
        var modes = new List<CascadeMode> { CascadeMode.Value };
        foreach (var mode in new[] { CascadeMode.PerSite, CascadeMode.PerZone, CascadeMode.PerSku })
        {
            var target = mode.DelegatesTo()!.Value;
            if (keyScopes.Allows(target) && IsBelow(target, scope))
            {
                modes.Add(mode);
            }
        }

        return modes;
    }



    /// <summary>
    /// True when <paramref name="scope"/> lies below <paramref name="ancestor"/> in a cascade chain.
    /// </summary>
    public static bool IsBelow(SettingScope scope, SettingScope ancestor)
    {
        var parent = scope.Parent();
        while (parent is { } p)
        {
            if (p == ancestor)
            {
                return true;
            }

            parent = p.Parent();
        }

        return false;
    }
}
