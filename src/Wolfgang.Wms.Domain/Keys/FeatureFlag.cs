// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// A feature flag: a boolean setting named <c>feature.&lt;name&gt;</c> with organisation → site cascade,
/// checked only at edges (endpoint filter, device task list, console menu). Ship-dark flags carry the
/// release in which they are removed, enforced at the prerelease gate.
/// </summary>
/// <param name="Name">Flag name without the <c>feature.</c> prefix, for example <c>bulk_picking</c>.</param>
/// <param name="RemoveInRelease">For ship-dark flags, the release that must remove the flag; null for permanent toggles.</param>
public sealed record FeatureFlag(string Name, string? RemoveInRelease = null)
{
    /// <summary>
    /// Flag name without the <c>feature.</c> prefix.
    /// </summary>
    public string Name { get; } = KeyName.Require(Name, nameof(Name));



    /// <summary>
    /// The setting name that stores this flag: <c>feature.&lt;name&gt;</c>.
    /// </summary>
    public string SettingName => "feature." + Name;
}
