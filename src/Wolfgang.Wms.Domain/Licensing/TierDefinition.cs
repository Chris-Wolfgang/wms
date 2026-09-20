// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// What one tier grants in one release (E79.1): an explicit feature set and a value for every limit.
/// </summary>
public sealed class TierDefinition
{
    /// <summary>
    /// Creates the definition.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public TierDefinition(LicenseTier tier, IEnumerable<LicenseFeature> features, IReadOnlyDictionary<string, LimitValue> limits)
    {
        ArgumentNullException.ThrowIfNull(features);
        Tier = tier ?? throw new ArgumentNullException(nameof(tier));
        Features = features.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        Limits = limits ?? throw new ArgumentNullException(nameof(limits));
    }



    /// <summary>The tier.</summary>
    public LicenseTier Tier { get; }

    /// <summary>The feature names granted.</summary>
    public IReadOnlySet<string> Features { get; }

    /// <summary>The value of each limit, by name.</summary>
    public IReadOnlyDictionary<string, LimitValue> Limits { get; }



    /// <summary>
    /// True when the tier grants the feature.
    /// </summary>
    public bool Grants(LicenseFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        return Features.Contains(feature.Name);
    }



    /// <summary>
    /// The value of a limit; unlimited when the table does not value it (which <see cref="TierTable.Verify"/> forbids).
    /// </summary>
    public LimitValue Limit(LicenseLimit limit)
    {
        ArgumentNullException.ThrowIfNull(limit);
        return Limits.TryGetValue(limit.Name, out var value) ? value : LimitValue.Unlimited;
    }
}
