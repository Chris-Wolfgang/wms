// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Identity;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// The roles before a database is configured (bootstrap): every call answers <c>auth.unavailable</c>.
/// <c>AddWmsDatabase</c> replaces it with the stored roles.
/// </summary>
public sealed class NoRoles : IRoles
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<RoleInfo>> ListAsync(CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<RoleInfo> FindAsync(long roleId, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<RoleInfo> CreateAsync(RoleDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<RoleInfo> UpdateAsync(long roleId, RoleDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<RoleInfo> CopyAsync(long roleId, string newName, string updatedBy, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task DeleteAsync(long roleId, string updatedBy, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<IReadOnlyList<RoleAssignmentInfo>> AssignmentsOfAsync(long userId, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<RoleAssignmentInfo> AssignAsync(long userId, long roleId, long? siteId, DateTimeOffset? expiresAt, string updatedBy, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task UnassignAsync(long assignmentId, string updatedBy, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GrantsOfAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<string>>([]);
    }



    /// <inheritdoc/>
    public Task<int> EnsureBuiltInAsync(PermissionCatalog catalog, CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }



    private static AuthException Unavailable()
    {
        return new AuthException(AuthErrorCodes.Unavailable, "Roles are unavailable until Wms:Database is configured and the schema installed.");
    }
}
