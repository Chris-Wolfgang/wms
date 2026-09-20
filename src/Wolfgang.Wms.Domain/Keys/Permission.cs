// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// A permission a role can hold, for example <c>picking.release</c>. Permissions are defined once in a
/// definitions class per module and enumerated to build the permission catalog (E10.1); roles are built from
/// the catalog and nothing else (E10.2).
/// </summary>
/// <param name="Name">Lower-case dotted identifier.</param>
/// <param name="Description">One line, user-facing, shown on the role editor.</param>
public sealed record Permission(string Name, string Description)
{
    /// <summary>
    /// The permission name.
    /// </summary>
    public string Name { get; } = KeyName.Require(Name, nameof(Name));



    /// <summary>
    /// What the permission allows.
    /// </summary>
    public string Description { get; } = RequireDescription(Description);



    /// <summary>
    /// The built-in roles that hold this permission from the start (E10.2); <see cref="BuiltInRole.Administrator"/>
    /// holds every permission whether listed or not.
    /// </summary>
    public IReadOnlyList<BuiltInRole> DefaultRoles { get; init; } = [];



    private static string RequireDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return description;
    }
}
