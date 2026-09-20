// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// The content of one issued key (E79.1, E79.3, E79.11) once its signature has been checked (Core does
/// that; the domain trusts what it is handed). A base key carries the tier, the organization and the
/// coverage; an add-on carries what it adds. Explicit entries win over the tier table: a feature listed here
/// is granted, a limit listed here replaces the tier's value (E79.1).
/// </summary>
/// <param name="SchemaVersion">The key format's own version, independent of product and API versions.</param>
/// <param name="KeyId">The unique id, used by <paramref name="Supersedes"/>.</param>
/// <param name="Kind">Base or add-on.</param>
/// <param name="Tier">The tier name (base keys).</param>
/// <param name="Organization">The organization the key is bound to.</param>
/// <param name="Coverage">The paid periods; empty for a perpetual key (the free tier), or for an add-on that co-terminates with the base.</param>
/// <param name="Features">Explicit feature grants.</param>
/// <param name="Limits">Explicit limit overrides by name.</param>
/// <param name="Devices">Devices an add-on adds to the limit.</param>
/// <param name="Supersedes">Key ids this key replaces.</param>
/// <param name="IssuedAt">When the key was issued.</param>
/// <param name="AllowancePercent">The overage allowance in percent (E79.4), or null for the default.</param>
/// <param name="AllowanceMinimumUnits">The overage allowance floor in units, or null for the default.</param>
/// <param name="GraceDays">The grace period in days, or null for the default.</param>
public sealed record LicenseKey
(
    int SchemaVersion,
    string KeyId,
    LicenseKeyKind Kind,
    string? Tier,
    string Organization,
    IReadOnlyList<CoveragePeriod> Coverage,
    IReadOnlyList<string> Features,
    IReadOnlyDictionary<string, LimitValue> Limits,
    int Devices,
    IReadOnlyList<string> Supersedes,
    DateOnly IssuedAt,
    int? AllowancePercent = null,
    int? AllowanceMinimumUnits = null,
    int? GraceDays = null
)
{
    /// <summary>
    /// The key format this build reads.
    /// </summary>
    public const int CurrentSchemaVersion = 1;



    /// <summary>
    /// True when the key has no coverage periods: perpetual for a base (the free tier), co-terminating with
    /// the base for an add-on.
    /// </summary>
    public bool IsPerpetual => Coverage.Count == 0;



    /// <summary>
    /// True when a release dated <paramref name="releaseDate"/> falls inside a covered period (or the key is perpetual).
    /// </summary>
    public bool Covers(DateOnly releaseDate)
    {
        return IsPerpetual || Coverage.Any(p => p.Contains(releaseDate));
    }
}
