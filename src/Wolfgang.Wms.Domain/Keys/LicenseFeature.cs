// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// A feature a license can grant, for example <c>workspace.insights</c> or <c>picking.bulk</c>, checked with
/// <c>ILicense.HasFeature(LicenseFeatures.Insights)</c> at the feature's edge (E79.2, E79.4).
/// </summary>
/// <param name="Name">Stable feature name.</param>
/// <param name="Description">One-line, user-facing description used in the generated feature comparison.</param>
public sealed record LicenseFeature(string Name, string Description)
{
    /// <summary>
    /// Stable feature name.
    /// </summary>
    public string Name { get; } = KeyName.Require(Name, nameof(Name));



    /// <summary>
    /// One-line, user-facing description.
    /// </summary>
    public string Description { get; } = RequireDescription(Description);



    private static string RequireDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return description;
    }
}
