// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// The scope tree the cascade walks (E6.3, E7.1): a scope's parent (where an unconfigured value is inherited
/// from) and its children (what a change flows down to). The organisation is the root. Until sites, zones
/// and SKUs exist as entities, <see cref="OrganizationOnlyScopeHierarchy"/> knows only that sites belong to
/// the organisation.
/// </summary>
public interface ISettingScopeHierarchy
{
    /// <summary>
    /// The scope <paramref name="scope"/> inherits from, or null at the root (or when the parent is unknown).
    /// </summary>
    Task<SettingScopeRef?> ParentAsync(SettingScopeRef scope, CancellationToken cancellationToken);



    /// <summary>
    /// The scopes directly under <paramref name="scope"/>.
    /// </summary>
    Task<IReadOnlyList<SettingScopeRef>> ChildrenAsync(SettingScopeRef scope, CancellationToken cancellationToken);
}
