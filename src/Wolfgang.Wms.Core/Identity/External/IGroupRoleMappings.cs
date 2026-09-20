// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.External;

/// <summary>
/// The group-to-role mappings of the providers (E11.2): access to the console is managed in the directory;
/// a provider user's roles are recomputed from these on every sign-in.
/// </summary>
public interface IGroupRoleMappings
{
    /// <summary>
    /// The mappings of a provider, by group then role.
    /// </summary>
    Task<IReadOnlyList<GroupRoleMappingInfo>> ListAsync(string provider, CancellationToken cancellationToken);



    /// <summary>
    /// Adds a mapping; the same (group, role, site) twice is a conflict.
    /// </summary>
    Task<GroupRoleMappingInfo> AddAsync(string provider, GroupRoleMappingDraft draft, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Removes a mapping; the users' roles follow on their next sign-in.
    /// </summary>
    Task RemoveAsync(long mappingId, string updatedBy, CancellationToken cancellationToken);
}
