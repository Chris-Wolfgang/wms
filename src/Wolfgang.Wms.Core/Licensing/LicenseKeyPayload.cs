// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The JSON form of a key's content (E79.3): what the vendor signs. Property names are snake_case
/// (<c>schema_version</c>, <c>key_id</c>); <see cref="LicenseKeyJson"/> validates and converts it to the
/// domain's <see cref="Domain.Licensing.LicenseKey"/>.
/// </summary>
internal sealed record LicenseKeyPayload
{
    /// <summary>The key format's version.</summary>
    public int SchemaVersion { get; init; }

    /// <summary>The unique key id.</summary>
    public string? KeyId { get; init; }

    /// <summary><c>base</c> or <c>add_on</c>.</summary>
    public string? Kind { get; init; }

    /// <summary>The tier (base keys).</summary>
    public string? Tier { get; init; }

    /// <summary>The organization the key is bound to.</summary>
    public string? Organization { get; init; }

    /// <summary>The paid periods.</summary>
    public IReadOnlyList<CoveragePeriodPayload>? Coverage { get; init; }

    /// <summary>Explicit feature grants.</summary>
    public IReadOnlyList<string>? Features { get; init; }

    /// <summary>Explicit limit values; null means unlimited.</summary>
    public IReadOnlyDictionary<string, int?>? Limits { get; init; }

    /// <summary>Devices an add-on adds.</summary>
    public int Devices { get; init; }

    /// <summary>Key ids this key replaces.</summary>
    public IReadOnlyList<string>? Supersedes { get; init; }

    /// <summary>When the key was issued.</summary>
    public DateOnly IssuedAt { get; init; }

    /// <summary>The overage allowance in percent, or null for the default.</summary>
    public int? AllowancePercent { get; init; }

    /// <summary>The allowance floor in units, or null for the default.</summary>
    public int? AllowanceMinimumUnits { get; init; }

    /// <summary>The grace period in days, or null for the default.</summary>
    public int? GraceDays { get; init; }
}
