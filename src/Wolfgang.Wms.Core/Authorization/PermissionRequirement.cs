// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Authorization;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// The requirement behind a <c>permission:&lt;name&gt;</c> policy (E10.1).
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Creates the requirement.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="permission"/> is null.</exception>
    public PermissionRequirement(Permission permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }



    /// <summary>
    /// The permission the endpoint needs.
    /// </summary>
    public Permission Permission { get; }
}
