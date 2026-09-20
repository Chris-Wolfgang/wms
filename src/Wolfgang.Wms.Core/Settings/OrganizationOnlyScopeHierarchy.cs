// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// The hierarchy before sites, zones and SKUs exist as entities (E6.3): a site's parent is the organisation,
/// a zone's or a SKU's site is unknown (so they inherit the default until E7), and nothing has children to
/// cascade to. Replaced by the entity-backed hierarchy in E7.1.
/// </summary>
public sealed class OrganizationOnlyScopeHierarchy : ISettingScopeHierarchy
{
    /// <inheritdoc/>
    public Task<SettingScopeRef?> ParentAsync(SettingScopeRef scope, CancellationToken cancellationToken)
    {
        SettingScopeRef? parent = scope.Type == SettingScope.Site ? SettingScopeRef.Organization : null;
        return Task.FromResult(parent);
    }



    /// <inheritdoc/>
    public Task<IReadOnlyList<SettingScopeRef>> ChildrenAsync(SettingScopeRef scope, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<SettingScopeRef>>([]);
    }
}
