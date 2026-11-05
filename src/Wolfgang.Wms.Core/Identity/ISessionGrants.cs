// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// The permission grants a session holds right now (E10.4). The session validator asks on every request and
/// replaces the grants the cookie was issued with, so an assignment, role or administrator flag that changed
/// or failed verification since sign-in takes effect on the next request rather than at the next sign-in.
/// </summary>
public interface ISessionGrants
{
    /// <summary>
    /// The user's grants (<see cref="Authorization.PermissionClaims"/> form) from rows that verify, or null
    /// when there is no store to ask and the session keeps the grants it carries.
    /// </summary>
    Task<IReadOnlyList<string>?> GrantsOfAsync(long userId, CancellationToken cancellationToken);
}
