// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.External;

/// <summary>
/// A group-to-role mapping as posted (E11.2).
/// </summary>
/// <param name="Group">The group identifier as the provider sends it.</param>
/// <param name="RoleId">The role to grant.</param>
/// <param name="SiteId">The site, or null for everywhere.</param>
public sealed record GroupRoleMappingDraft(string Group, long RoleId, long? SiteId = null);
