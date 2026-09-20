// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// Endpoint metadata naming the permission it requires (E10.1), so the catalog, the documentation and the
/// architecture test can read what each endpoint declares.
/// </summary>
public sealed class PermissionMetadata
{
    /// <summary>
    /// Creates the metadata.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="permission"/> is null.</exception>
    public PermissionMetadata(Permission permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }



    /// <summary>
    /// The permission the endpoint requires.
    /// </summary>
    public Permission Permission { get; }
}
