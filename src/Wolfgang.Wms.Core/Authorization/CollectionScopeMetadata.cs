// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// Marks a collection endpoint without a <c>siteId</c> in its route that filters its rows through
/// <see cref="SiteScope"/> (E16.3): a caller who holds the permission at any site is admitted and sees that
/// site's rows, instead of needing an organisation-level grant.
/// </summary>
public sealed class CollectionScopeMetadata
{
    /// <summary>
    /// Creates the marker for <paramref name="permission"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="permission"/> is null.</exception>
    public CollectionScopeMetadata(Permission permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }



    /// <summary>The permission the endpoint requires somewhere.</summary>
    public Permission Permission { get; }
}
