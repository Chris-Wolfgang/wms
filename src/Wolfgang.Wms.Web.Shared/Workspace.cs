// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Web.Shared;

/// <summary>
/// One console workspace (E82.4): its own project, route, navigation and layout, shown only to roles holding
/// its <see cref="Permission"/>, and entered only when the license includes its <see cref="LicenseFeature"/>.
/// </summary>
/// <param name="Name">Short lower-case identifier (<c>configure</c>); also the route segment.</param>
/// <param name="Title">Display name (<c>Configure</c>).</param>
/// <param name="Description">One sentence for the chooser and the docs.</param>
/// <param name="LicenseFeature">The license feature checked at the workspace entry.</param>
/// <param name="Permission">The permission a role must hold to see the workspace.</param>
/// <param name="FreeTier">True when the v1 free tier includes the workspace.</param>
public sealed record Workspace
(
    string Name,
    string Title,
    string Description,
    LicenseFeature LicenseFeature,
    Permission Permission,
    bool FreeTier
)
{
    /// <summary>
    /// Route segment; validated as a key name.
    /// </summary>
    public string Name { get; } = KeyName.Require(Name, nameof(Name));



    /// <summary>
    /// The absolute route of the workspace root (<c>/configure</c>).
    /// </summary>
    public string Route => "/" + Name;
}
