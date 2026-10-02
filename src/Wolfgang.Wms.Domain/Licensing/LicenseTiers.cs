// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// The tiers that exist (E79.1). Tiers are only ever added: a shipped name stays valid in every later
/// release, so any valid key resolves.
/// </summary>
public static class LicenseTiers
{
    /// <summary>The compiled-in tier every install has without a key (E79.2).</summary>
    public static LicenseTier Free { get; } = new("free", 0);

    /// <summary>The paid tier with every v1 feature.</summary>
    public static LicenseTier Pro { get; } = new("pro", 1);

    /// <summary>The paid tier with the highest limits; its commercial definition is E77's.</summary>
    public static LicenseTier Enterprise { get; } = new("enterprise", 2);



    /// <summary>
    /// Every tier, lowest first.
    /// </summary>
    public static IReadOnlyList<LicenseTier> All { get; } = [Free, Pro, Enterprise];



    /// <summary>
    /// The tier of that name, or null for a name no release knows.
    /// </summary>
    public static LicenseTier? Find(string? name)
    {
        return All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));
    }
}
