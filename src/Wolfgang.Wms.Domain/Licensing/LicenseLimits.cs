// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// Every license limit (E79.1). A tier table must value each of them in every tier, so a valid key always
/// resolves every limit.
/// </summary>
public static class LicenseLimits
{
    /// <summary>Sites.</summary>
    public static LicenseLimit Sites { get; } = new("sites", "Sites");

    /// <summary>Devices active in the trailing window (heartbeat or login seen; E79.4), not enrolled devices.</summary>
    public static LicenseLimit Devices { get; } = new("devices", "Connected devices (active in the last 30 days)");

    /// <summary>Console users and pickers.</summary>
    public static LicenseLimit Users { get; } = new("users", "Users and pickers");

    /// <summary>Totes a picker may carry at once (E79.9).</summary>
    public static LicenseLimit MaxTotesPerPicker { get; } = new("max_totes_per_picker", "Totes per picker");



    /// <summary>
    /// Every limit, in catalogue order.
    /// </summary>
    public static IReadOnlyList<LicenseLimit> All { get; } = [Sites, Devices, Users, MaxTotesPerPicker];



    /// <summary>
    /// The limit of that name, or null.
    /// </summary>
    public static LicenseLimit? Find(string? name)
    {
        return All.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.Ordinal));
    }
}
