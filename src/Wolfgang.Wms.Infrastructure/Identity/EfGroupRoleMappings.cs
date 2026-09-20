// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Identity.External;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// <see cref="IGroupRoleMappings"/> over <c>core.group_role_mapping</c> (E11.2).
/// </summary>
public sealed class EfGroupRoleMappings : IGroupRoleMappings
{
    private readonly WmsDbContext _context;
    private readonly TimeProvider _timeProvider;



    /// <summary>
    /// Creates the mappings.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfGroupRoleMappings(WmsDbContext context, TimeProvider timeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    public async Task<IReadOnlyList<GroupRoleMappingInfo>> ListAsync(string provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var mappings = await _context.GroupRoleMappings.Include(m => m.Role).AsNoTracking()
            .Where(m => m.Provider == provider)
            .OrderBy(m => m.GroupKey).ThenBy(m => m.Role!.Name).ThenBy(m => m.SiteId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return mappings.Select(View).ToList();
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="AuthException">The role does not exist, the group is blank, or the mapping exists.</exception>
    public async Task<GroupRoleMappingInfo> AddAsync(string provider, GroupRoleMappingDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(updatedBy);

        var group = draft.Group?.Trim() ?? string.Empty;
        if (group.Length is 0 or > GroupRoleMapping.GroupLength)
        {
            throw new AuthException(AuthErrorCodes.MappingRejected, $"The group must be 1 to {GroupRoleMapping.GroupLength} characters.");
        }

        var role = await _context.Roles.SingleOrDefaultAsync(r => r.Id == draft.RoleId, cancellationToken).ConfigureAwait(false)
            ?? throw new AuthException(RoleErrorCodes.RoleNotFound, $"Role {draft.RoleId} does not exist.");
        if (await _context.GroupRoleMappings.AnyAsync(m => m.Provider == provider && m.GroupKey == group && m.RoleId == draft.RoleId && m.SiteId == draft.SiteId, cancellationToken).ConfigureAwait(false))
        {
            throw new AuthException(AuthErrorCodes.MappingRejected, "That group is already mapped to that role at that scope.");
        }

        var mapping = new GroupRoleMapping { Provider = provider, GroupKey = group, RoleId = role.Id, SiteId = draft.SiteId, UpdatedAt = _timeProvider.GetUtcNow(), UpdatedBy = updatedBy, Role = role };
        _context.GroupRoleMappings.Add(mapping);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return View(mapping);
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="updatedBy"/> is null.</exception>
    /// <exception cref="AuthException">The mapping does not exist.</exception>
    public async Task RemoveAsync(long mappingId, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(updatedBy);

        var mapping = await _context.GroupRoleMappings.SingleOrDefaultAsync(m => m.Id == mappingId, cancellationToken).ConfigureAwait(false)
            ?? throw new AuthException(AuthErrorCodes.MappingNotFound, $"Mapping {mappingId} does not exist.");
        _context.GroupRoleMappings.Remove(mapping);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }



    private static GroupRoleMappingInfo View(GroupRoleMapping mapping)
    {
        return new GroupRoleMappingInfo(mapping.Id, mapping.Provider, mapping.GroupKey, mapping.RoleId, mapping.Role?.Name ?? string.Empty, mapping.SiteId);
    }
}
