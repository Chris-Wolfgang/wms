// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// Names and descriptions of the built-in roles (E10.2).
/// </summary>
public static class BuiltInRoles
{
    /// <summary>
    /// Every built-in role.
    /// </summary>
    public static IReadOnlyList<BuiltInRole> All { get; } = [BuiltInRole.Administrator, BuiltInRole.Supervisor, BuiltInRole.Resolver, BuiltInRole.Support, BuiltInRole.Viewer];



    /// <summary>
    /// The stored key of a role (<c>administrator</c>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role"/> is not a built-in role.</exception>
    public static string Key(this BuiltInRole role)
    {
        return role switch
        {
            BuiltInRole.Administrator => "administrator",
            BuiltInRole.Supervisor => "supervisor",
            BuiltInRole.Resolver => "resolver",
            BuiltInRole.Support => "support",
            BuiltInRole.Viewer => "viewer",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown built-in role."),
        };
    }



    /// <summary>
    /// The display name of a role.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role"/> is not a built-in role.</exception>
    public static string DisplayName(this BuiltInRole role)
    {
        return role switch
        {
            BuiltInRole.Administrator => "Administrator",
            BuiltInRole.Supervisor => "Supervisor",
            BuiltInRole.Resolver => "Resolver",
            BuiltInRole.Support => "Support",
            BuiltInRole.Viewer => "Viewer",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown built-in role."),
        };
    }



    /// <summary>
    /// What the role is for.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role"/> is not a built-in role.</exception>
    public static string Description(this BuiltInRole role)
    {
        return role switch
        {
            BuiltInRole.Administrator => "Everything, everywhere.",
            BuiltInRole.Supervisor => "Runs the floor: live operations, resolution and reports.",
            BuiltInRole.Resolver => "The resolution lane only: flagged totes, shorts, release to pack, tote inquiry, messages to supervisors.",
            BuiltInRole.Support => "Reads settings and reports to help users; changes nothing.",
            BuiltInRole.Viewer => "Reads reports.",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown built-in role."),
        };
    }
}
