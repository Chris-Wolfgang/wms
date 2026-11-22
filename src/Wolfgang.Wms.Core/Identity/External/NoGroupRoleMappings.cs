// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.External;

/// <summary>
/// The mappings of a host without a database (E11.2): reads are empty, writes are unavailable.
/// </summary>
public sealed class NoGroupRoleMappings : IGroupRoleMappings
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<GroupRoleMappingInfo>> ListAsync(string provider, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<GroupRoleMappingInfo>>([]);
    }



    /// <inheritdoc/>
    /// <exception cref="AuthException">Always: <see cref="AuthErrorCodes.Unavailable"/>.</exception>
    public Task<GroupRoleMappingInfo> AddAsync(string provider, GroupRoleMappingDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        throw new AuthException(AuthErrorCodes.Unavailable, "Group mappings are unavailable until the database is configured.");
    }



    /// <inheritdoc/>
    /// <exception cref="AuthException">Always: <see cref="AuthErrorCodes.Unavailable"/>.</exception>
    public Task RemoveAsync(long mappingId, string updatedBy, CancellationToken cancellationToken)
    {
        throw new AuthException(AuthErrorCodes.Unavailable, "Group mappings are unavailable until the database is configured.");
    }
}
