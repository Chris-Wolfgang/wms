// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// The compiled-in free tier (E79.2): the base key every install has without pasting one. Never expires,
/// binds to no organization (add-on keys of any organization stack on it), grants exactly what the free
/// tier of this release's table grants, and carries its own overage values.
/// </summary>
public static class FreeLicense
{
    /// <summary>
    /// The key id of the free base key.
    /// </summary>
    public const string KeyId = "free";



    /// <summary>
    /// The free tier's overage allowance (E79.4): 10 percent, at least 1 unit — one device over is a warning
    /// with time to act, not a wall.
    /// </summary>
    public const int AllowancePercent = 10;

    /// <summary>The free tier's allowance floor in units.</summary>
    public const int AllowanceMinimumUnits = 1;

    /// <summary>The free tier's grace period in days.</summary>
    public const int GraceDays = 14;



    /// <summary>
    /// The key.
    /// </summary>
    public static LicenseKey Key { get; } = new
    (
        LicenseKey.CurrentSchemaVersion,
        KeyId,
        LicenseKeyKind.Base,
        LicenseTiers.Free.Name,
        Organization: string.Empty,
        Coverage: [],
        Features: [],
        Limits: new Dictionary<string, LimitValue>(StringComparer.Ordinal),
        Devices: 0,
        Supersedes: [],
        IssuedAt: ReleaseInfo.ReleaseDate,
        AllowancePercent,
        AllowanceMinimumUnits,
        GraceDays
    );
}
