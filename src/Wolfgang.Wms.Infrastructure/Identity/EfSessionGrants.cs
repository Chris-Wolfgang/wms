// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Integrity;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// <see cref="ISessionGrants"/> over <c>core.user</c> and the role assignments (E10.4): on every request the
/// session holds what the rows that verify grant now. The local administrator holds every permission
/// everywhere (E10.1); everyone's active assignments add theirs (E10.2, E10.3), each assignment and role
/// verified (<see cref="IRoles.GrantsOfAsync"/>). A user row that fails grants nothing.
/// </summary>
public sealed class EfSessionGrants : ISessionGrants
{
    private readonly WmsDbContext _context;
    private readonly IRoles _roles;
    private readonly IIntegritySigner _signer;
    private readonly TimeProvider _timeProvider;



    /// <summary>
    /// Creates the grants.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfSessionGrants(WmsDbContext context, IRoles roles, IIntegritySigner signer, TimeProvider timeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _roles = roles ?? throw new ArgumentNullException(nameof(roles));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }



    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>?> GrantsOfAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null || !await _signer.IsValidAsync(user, UserConfiguration.Schema + "." + UserConfiguration.Table, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        return Combine(user.IsLocalAdmin, await _roles.GrantsOfAsync(user.Id, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false));
    }



    /// <summary>
    /// A user's grants: everything everywhere first for a local administrator, then the assignments' grants,
    /// without duplicates.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="grants"/> is null.</exception>
    public static IReadOnlyList<string> Combine(bool isLocalAdmin, IReadOnlyList<string> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);

        var all = isLocalAdmin ? grants.Prepend(PermissionClaims.OrganizationGrant(PermissionClaims.Wildcard)) : grants;
        return all.Distinct(StringComparer.Ordinal).ToList();
    }
}
