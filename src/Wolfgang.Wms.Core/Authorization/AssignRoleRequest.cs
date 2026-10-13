// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// Body of <c>POST /auth/users/{userId}/roles</c> (E10.3).
/// </summary>
/// <param name="RoleId">The role to assign.</param>
/// <param name="SiteId">The site, or null for everywhere.</param>
/// <param name="ExpiresAt">When the assignment stops applying, or null for no expiry.</param>
public sealed record AssignRoleRequest(long RoleId, long? SiteId = null, DateTimeOffset? ExpiresAt = null);
