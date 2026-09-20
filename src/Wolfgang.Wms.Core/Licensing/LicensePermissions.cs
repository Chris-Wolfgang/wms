// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The <c>license</c> module's permissions (E79.6).
/// </summary>
public static class LicensePermissions
{
    /// <summary>
    /// See the license, the usage and the comparison.
    /// </summary>
    public static readonly Permission Read = new("license.read", "View the license, usage against limits and the tier comparison") { DefaultRoles = [BuiltInRole.Supervisor, BuiltInRole.Support, BuiltInRole.Viewer] };



    /// <summary>
    /// Install and remove keys.
    /// </summary>
    public static readonly Permission Manage = new("license.manage", "Install and remove license keys");



    /// <summary>
    /// Every permission of the module.
    /// </summary>
    public static IReadOnlyList<Permission> All { get; } = [Read, Manage];
}
