// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// One row of <c>core.role_permission</c> (E10.2): a permission a role grants, by catalog name (<c>*</c> for
/// every permission).
/// </summary>
public sealed class RolePermission
{
    /// <summary>Longest permission name.</summary>
    public const int PermissionNameLength = 128;



    /// <summary>The server-assigned identifier.</summary>
    public long Id { get; set; }



    /// <summary>The role.</summary>
    public long RoleId { get; set; }



    /// <summary>The permission name.</summary>
    public string PermissionName { get; set; } = string.Empty;
}
