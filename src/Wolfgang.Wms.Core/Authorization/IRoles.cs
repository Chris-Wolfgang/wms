// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Identity;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// Roles and their assignments (E10.2, E10.3). A role is a named set of catalog permissions; the built-in
/// ones are read-only (copy one to edit it) and their permission sets follow the catalog. An assignment
/// gives a user a role everywhere (no site) or at one site, optionally until a date; sign-in turns the
/// active assignments into grants. Implemented over the database; before one exists every call answers
/// <c>auth.unavailable</c>.
/// </summary>
public interface IRoles
{
    /// <summary>
    /// Every role, built-in first, then by name.
    /// </summary>
    Task<IReadOnlyList<RoleInfo>> ListAsync(CancellationToken cancellationToken);



    /// <summary>
    /// One role.
    /// </summary>
    /// <exception cref="AuthException">No such role.</exception>
    Task<RoleInfo> FindAsync(long roleId, CancellationToken cancellationToken);



    /// <summary>
    /// Creates a role from catalog permissions.
    /// </summary>
    /// <exception cref="AuthException">The name is taken, or a permission is not in the catalog.</exception>
    Task<RoleInfo> CreateAsync(RoleDraft draft, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Replaces a role's name, description and permissions.
    /// </summary>
    /// <exception cref="AuthException">No such role, a built-in role, the name is taken, or a permission is not in the catalog.</exception>
    Task<RoleInfo> UpdateAsync(long roleId, RoleDraft draft, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Creates an editable copy of a role under a new name.
    /// </summary>
    /// <exception cref="AuthException">No such role, or the name is taken.</exception>
    Task<RoleInfo> CopyAsync(long roleId, string newName, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Deletes a role and its assignments.
    /// </summary>
    /// <exception cref="AuthException">No such role, or a built-in role.</exception>
    Task DeleteAsync(long roleId, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// The assignments of a user, active and expired alike.
    /// </summary>
    Task<IReadOnlyList<RoleAssignmentInfo>> AssignmentsOfAsync(long userId, CancellationToken cancellationToken);



    /// <summary>
    /// Assigns a role to a user everywhere (<paramref name="siteId"/> null) or at one site, optionally until
    /// <paramref name="expiresAt"/>; an existing assignment of the same role at the same scope is replaced.
    /// </summary>
    /// <exception cref="AuthException">No such user or role.</exception>
    Task<RoleAssignmentInfo> AssignAsync(long userId, long roleId, long? siteId, DateTimeOffset? expiresAt, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Removes an assignment.
    /// </summary>
    /// <exception cref="AuthException">No such assignment.</exception>
    Task UnassignAsync(long assignmentId, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// The grants (<see cref="PermissionClaims"/>) of a user's active assignments at <paramref name="now"/>:
    /// expired ones are skipped.
    /// </summary>
    Task<IReadOnlyList<string>> GrantsOfAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken);



    /// <summary>
    /// Creates the built-in roles that are missing and refreshes their permission sets from the catalog
    /// (E10.2); returns the number of roles created.
    /// </summary>
    Task<int> EnsureBuiltInAsync(PermissionCatalog catalog, CancellationToken cancellationToken);
}
