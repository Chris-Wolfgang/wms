// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.External;

/// <summary>
/// One directory group mapped to one role (E11.2), everywhere or at one site.
/// </summary>
/// <param name="Id">The server-assigned identifier.</param>
/// <param name="Provider">The provider whose groups these are.</param>
/// <param name="Group">The group identifier as the provider sends it (an object id, a name, a DN).</param>
/// <param name="RoleId">The role granted.</param>
/// <param name="RoleName">The role's name.</param>
/// <param name="SiteId">The site the role applies at, or null for everywhere.</param>
public sealed record GroupRoleMappingInfo(long Id, string Provider, string Group, long RoleId, string RoleName, long? SiteId);
