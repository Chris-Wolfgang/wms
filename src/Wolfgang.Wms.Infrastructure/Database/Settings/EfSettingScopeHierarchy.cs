// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Infrastructure.Database.Settings;

/// <summary>
/// The settings cascade over the stored sites and zones (E16.1, E16.2): the organisation's children are
/// every site, a site's children are its zones (active and retired alike, so their effective values stay
/// consistent for their history), a zone's parent is its site and a site's parent is the organisation. A
/// SKU scope has no parent or children here; a zone that does not exist has no parent.
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
    public async Task<SettingScopeRef?> ParentAsync(SettingScopeRef scope, CancellationToken cancellationToken)
    {
        switch (scope.Type)
        {
            case SettingScope.Site:
                return SettingScopeRef.Organization;
            case SettingScope.Zone:
                var siteId = await _context.Zones.AsNoTracking().Where(z => z.Id == scope.Id).Select(z => (long?)z.SiteId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                return siteId is { } id ? SettingScopeRef.Site(id) : null;
            default:
                return null;
        }
    }



    /// <inheritdoc/>
    public async Task<IReadOnlyList<SettingScopeRef>> ChildrenAsync(SettingScopeRef scope, CancellationToken cancellationToken)
    {
        switch (scope.Type)
        {
            case SettingScope.Organization:
                var sites = await _context.Sites.AsNoTracking().OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
                return sites.Select(SettingScopeRef.Site).ToList();
            case SettingScope.Site:
                var zones = await _context.Zones.AsNoTracking().Where(z => z.SiteId == scope.Id).OrderBy(z => z.Id).Select(z => z.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
                return zones.Select(SettingScopeRef.Zone).ToList();
            default:
                return [];
        }
    }
}
