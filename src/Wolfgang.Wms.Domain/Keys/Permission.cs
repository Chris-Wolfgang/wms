// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// A permission a role can hold, for example <c>picking.release</c>. Permissions are defined once in a
/// definitions class per module and enumerated to build the permission catalog (E10.1).
/// </summary>
/// <param name="Name">Stable permission name.</param>
/// <param name="Description">One-line, user-facing description for the role editor and generated docs.</param>
public sealed record Permission(string Name, string Description)
{
    /// <summary>
    /// Stable permission name.
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
