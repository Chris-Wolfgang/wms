// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Infrastructure.Database.Settings;

/// <summary>
/// The settings cascade over the stored sites (E16.1): the organisation's children are every site, active
/// and retired (a retired site keeps its effective values consistent for its history), and a site's parent is
/// the organisation. Zones (E16.2) will sit below their site; until then a site has no children and a zone or
/// SKU scope has no parent, as the no-database hierarchy answers.
/// </summary>
public sealed class EfSettingScopeHierarchy : ISettingScopeHierarchy
{
    private readonly WmsDbContext _context;



    /// <summary>
    /// Creates the hierarchy over the context.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public EfSettingScopeHierarchy(WmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }



    /// <inheritdoc/>
    public Task<SettingScopeRef?> ParentAsync(SettingScopeRef scope, CancellationToken cancellationToken)
    {
        SettingScopeRef? parent = scope.Type == SettingScope.Site ? SettingScopeRef.Organization : null;
        return Task.FromResult(parent);
    }



    /// <inheritdoc/>
    public async Task<IReadOnlyList<SettingScopeRef>> ChildrenAsync(SettingScopeRef scope, CancellationToken cancellationToken)
    {
        if (scope.Type != SettingScope.Organization)
        {
            return [];
        }

        var ids = await _context.Sites.AsNoTracking().OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        return ids.Select(SettingScopeRef.Site).ToList();
    }
}
