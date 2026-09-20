// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// One role assignment (E10.3): a user holds a role everywhere or at one site, optionally until a date.
/// </summary>
/// <param name="Id">The server-assigned identifier.</param>
/// <param name="UserId">The user.</param>
/// <param name="RoleId">The role.</param>
/// <param name="RoleName">The role's name.</param>
/// <param name="SiteId">The site, or null for everywhere (the organisation).</param>
/// <param name="ExpiresAt">When the assignment stops applying, or null for no expiry.</param>
/// <param name="Expired">True when <see cref="ExpiresAt"/> has passed.</param>
public sealed record RoleAssignmentInfo(long Id, long UserId, long RoleId, string RoleName, long? SiteId, DateTimeOffset? ExpiresAt, bool Expired);
