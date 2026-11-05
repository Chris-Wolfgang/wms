// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// Composes the effective license from the installed keys (E79.1, E79.5, E79.11): one base (the newest
/// issued base whose signature checked; the free key when none), every add-on bound to the same
/// organization and valid while the base is in coverage, superseded keys ignored, explicit entries over the
/// tier table. Pure: give it the keys and the release date.
/// </summary>
public static class LicenseComposer
{
    /// <summary>The default overage allowance in percent (E79.4).</summary>
    public const int DefaultAllowancePercent = 10;

    /// <summary>The default allowance floor in units.</summary>
    public const int DefaultAllowanceMinimumUnits = 2;

    /// <summary>The default grace period in days.</summary>
    public const int DefaultGraceDays = 30;



    /// <summary>
    /// The effective license for <paramref name="installed"/> (keys that could not be verified arrive with
    /// <see cref="KeyStatus.Invalid"/> and a null key) on a release dated <paramref name="releaseDate"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static EffectiveLicense Compose(TierTable table, IReadOnlyList<InstalledKey> installed, DateOnly releaseDate)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(installed);

        var superseded = installed.Where(k => k.Key is not null).SelectMany(k => k.Key!.Supersedes).ToHashSet(StringComparer.Ordinal);
        var readable = installed.Where(k => k.Key is not null).ToList();
        var bases = readable.Where(k => k.Key!.Kind == LicenseKeyKind.Base && !superseded.Contains(k.KeyId)).OrderByDescending(k => k.Key!.IssuedAt).ThenBy(k => k.KeyId, StringComparer.Ordinal).ToList();
        var baseKey = bases.FirstOrDefault()?.Key ?? FreeLicense.Key;
        var coverage = CoverageStatus.Perpetual;
        if (!baseKey.IsPerpetual)
        {
            coverage = baseKey.Covers(releaseDate) ? CoverageStatus.Covered : CoverageStatus.Lapsed;
        }

        var coveredUntil = baseKey.IsPerpetual ? (DateOnly?)null : baseKey.Coverage.Max(p => p.To);
        var tier = LicenseTiers.Find(baseKey.Tier) ?? LicenseTiers.Free;
        var (features, limits) = Grants(table.Find(tier) ?? table.Find(LicenseTiers.Free)!, baseKey);
        var included = limits[LicenseLimits.Devices.Name];
        var purchased = 0;
        var keys = new List<InstalledKey>();
        foreach (var entry in installed)
        {
            var (status, reason) = Classify(entry, baseKey, superseded, coverage, releaseDate);
            keys.Add(entry with { Status = status, Reason = reason });
            if (status != KeyStatus.Active || entry.Key!.Kind != LicenseKeyKind.AddOn)
            {
                continue;
            }

            features.UnionWith(entry.Key.Features.Where(f => LicenseFeatures.Find(f) is not null));
            purchased += entry.Key.Devices;
            foreach (var (name, value) in entry.Key.Limits.Where(l => LicenseLimits.Find(l.Key) is not null && !string.Equals(l.Key, LicenseLimits.Devices.Name, StringComparison.Ordinal)))
            {
                limits[name] = limits[name].IsReducedBy(value) ? limits[name] : value;   // an add-on only ever raises
            }
        }

        limits[LicenseLimits.Devices.Name] = included.Plus(purchased);
        return new EffectiveLicense
        (
            tier,
            baseKey.Organization,
            coverage,
            coveredUntil,
            features,
            limits,
            included.IsUnlimited ? 0 : included.Value!.Value,
            purchased,
            baseKey.AllowancePercent ?? DefaultAllowancePercent,
            baseKey.AllowanceMinimumUnits ?? DefaultAllowanceMinimumUnits,
            baseKey.GraceDays ?? DefaultGraceDays,
            keys
        );
    }



    /// <summary>
    /// The tier's grants with the base key's explicit entries applied: a listed feature is granted, a listed
    /// limit replaces the tier's value.
    /// </summary>
    private static (HashSet<string> Features, Dictionary<string, LimitValue> Limits) Grants(TierDefinition definition, LicenseKey baseKey)
    {
        var features = new HashSet<string>(definition.Features, StringComparer.Ordinal);
        features.UnionWith(baseKey.Features.Where(f => LicenseFeatures.Find(f) is not null));
        var limits = new Dictionary<string, LimitValue>(definition.Limits, StringComparer.Ordinal);
        foreach (var (name, value) in baseKey.Limits.Where(l => LicenseLimits.Find(l.Key) is not null))
        {
            limits[name] = value;
        }

        return (features, limits);
    }



    private static (KeyStatus Status, string? Reason) Classify(InstalledKey entry, LicenseKey baseKey, HashSet<string> superseded, CoverageStatus coverage, DateOnly releaseDate)
    {
        if (entry.Key is null)
        {
            return (KeyStatus.Invalid, entry.Reason ?? "the key could not be verified");
        }

        if (superseded.Contains(entry.KeyId))
        {
            return (KeyStatus.Superseded, "replaced by a later key");
        }

        if (entry.Key.Kind == LicenseKeyKind.Base)
        {
            if (!ReferenceEquals(entry.Key, baseKey))
            {
                return (KeyStatus.ExtraBase, $"a newer base key ({baseKey.KeyId}) is in force; one base counts");
            }

            return coverage == CoverageStatus.Lapsed ? (KeyStatus.Lapsed, $"coverage ended {baseKey.Coverage.Max(p => p.To):yyyy-MM-dd}; this release ({releaseDate:yyyy-MM-dd}) is not covered") : (KeyStatus.Active, null);
        }

        if (baseKey.Organization.Length > 0 && !string.Equals(entry.Key.Organization, baseKey.Organization, StringComparison.Ordinal))
        {
            return (KeyStatus.ForeignOrganization, $"issued to '{entry.Key.Organization}', not '{baseKey.Organization}'");
        }

        if (coverage == CoverageStatus.Lapsed)
        {
            return (KeyStatus.Lapsed, "the base key's coverage lapsed; the stack is frozen together");
        }

        if (!entry.Key.Covers(releaseDate))
        {
            return (KeyStatus.Lapsed, $"its coverage ended {entry.Key.Coverage.Max(p => p.To):yyyy-MM-dd}");
        }

        return (KeyStatus.Active, null);
    }
}
