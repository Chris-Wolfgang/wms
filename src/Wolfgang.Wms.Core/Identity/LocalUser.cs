// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// A local account as the rest of the product sees it (E9): never the password hash.
/// </summary>
/// <param name="Id">The server-assigned identifier.</param>
/// <param name="UserName">The sign-in name.</param>
/// <param name="DisplayName">The name shown on screen.</param>
/// <param name="MustChangePassword">True until the user has replaced the bootstrap or reset password (E9.1).</param>
/// <param name="IsLocalAdmin">True for the break-glass administrator (E9.3), who can be disabled but never deleted.</param>
/// <param name="IsDisabled">True when sign-in is refused.</param>
/// <param name="Grants">The permission grants (<see cref="Wolfgang.Wms.Core.Authorization.PermissionClaims"/>) the session carries: everything for the local administrator, the roles' grants for others (E10.2).</param>
public sealed record LocalUser(long Id, string UserName, string DisplayName, bool MustChangePassword, bool IsLocalAdmin, bool IsDisabled, IReadOnlyList<string> Grants);
