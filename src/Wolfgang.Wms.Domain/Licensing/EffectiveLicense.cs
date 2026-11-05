// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// What an install is entitled to right now (E79.1, E79.11): the base key's tier resolved through this
/// release's tier table, plus every valid add-on, plus explicit entries — an explicit set of features and a
/// value for every limit.
/// </summary>
public sealed class EffectiveLicense
{
    /// <summary>
    /// Creates the license.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EffectiveLicense(LicenseTier tier, string organization, CoverageStatus coverage, DateOnly? coveredUntil, IReadOnlySet<string> features, IReadOnlyDictionary<string, LimitValue> limits, int includedDevices, int purchasedDevices, int allowancePercent, int allowanceMinimumUnits, int graceDays, IReadOnlyList<InstalledKey> keys)
    {
        Tier = tier ?? throw new ArgumentNullException(nameof(tier));
        Organization = organization ?? throw new ArgumentNullException(nameof(organization));
        Coverage = coverage;
        CoveredUntil = coveredUntil;
        Features = features ?? throw new ArgumentNullException(nameof(features));
        Limits = limits ?? throw new ArgumentNullException(nameof(limits));
        IncludedDevices = includedDevices;
        PurchasedDevices = purchasedDevices;
        AllowancePercent = allowancePercent;
        AllowanceMinimumUnits = allowanceMinimumUnits;
        GraceDays = graceDays;
        Keys = keys ?? throw new ArgumentNullException(nameof(keys));
    }



    /// <summary>The tier.</summary>
    public LicenseTier Tier { get; }

    /// <summary>The organization the base key binds to (empty for the free tier).</summary>
    public string Organization { get; }

    /// <summary>Whether this release is covered.</summary>
    public CoverageStatus Coverage { get; }

    /// <summary>The last covered day, or null when perpetual.</summary>
    public DateOnly? CoveredUntil { get; }

    /// <summary>The feature names granted.</summary>
    public IReadOnlySet<string> Features { get; }

    /// <summary>The value of every limit, by name.</summary>
    public IReadOnlyDictionary<string, LimitValue> Limits { get; }

    /// <summary>Devices the tier includes.</summary>
    public int IncludedDevices { get; }

    /// <summary>Devices bought through add-ons.</summary>
    public int PurchasedDevices { get; }

    /// <summary>The overage allowance in percent (E79.4).</summary>
    public int AllowancePercent { get; }

    /// <summary>The overage allowance floor in units.</summary>
    public int AllowanceMinimumUnits { get; }

    /// <summary>The grace period in days.</summary>
    public int GraceDays { get; }

    /// <summary>Every installed key with its status.</summary>
    public IReadOnlyList<InstalledKey> Keys { get; }



    /// <summary>
    /// True when the feature is granted.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="feature"/> is null.</exception>
    public bool HasFeature(LicenseFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        return Features.Contains(feature.Name);
    }



    /// <summary>
    /// The value of a limit.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="limit"/> is null.</exception>
    public LimitValue Limit(LicenseLimit limit)
    {
        ArgumentNullException.ThrowIfNull(limit);
        return Limits.TryGetValue(limit.Name, out var value) ? value : LimitValue.Unlimited;
    }



    /// <summary>
    /// The units of overage the allowance permits for <paramref name="limit"/>: max(percent, floor) of the
    /// ceiling; none for an unlimited value.
    /// </summary>
    public int AllowanceUnits(LicenseLimit limit)
    {
        var value = Limit(limit);
        if (value.IsUnlimited)
        {
            return 0;
        }

        var percent = (int)Math.Ceiling(value.Value!.Value * AllowancePercent / 100.0);
        return Math.Max(percent, AllowanceMinimumUnits);
    }
}
