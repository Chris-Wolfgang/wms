// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Zones;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Zones;

/// <summary>
/// <see cref="IZones"/> over <c>layout.zone</c> (E16.2). Codes are unique within the site without regard to
/// case; assigned resolvers must be existing users; deactivating a zone asks <see cref="IOpenZoneGroups"/>
/// first; every write goes through the audited context (E6.4) and bumps the row version (E5.1).
/// </summary>
public sealed class EfZones : IZones
{
    private readonly WmsDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IOpenZoneGroups _openGroups;



    /// <summary>
    /// Creates the store.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfZones(WmsDbContext context, TimeProvider timeProvider, IOpenZoneGroups openGroups)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _openGroups = openGroups ?? throw new ArgumentNullException(nameof(openGroups));
    }



    /// <inheritdoc/>
    public async Task<IReadOnlyList<ZoneInfo>> ListAsync(long siteId, CancellationToken cancellationToken)
    {
        await RequireSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        var rows = await _context.Zones.AsNoTracking().Include(z => z.Resolvers).Where(z => z.SiteId == siteId).OrderBy(z => z.CodeNormalized).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(z => z.ToInfo()).ToList();
    }



    /// <inheritdoc/>
    public async Task<ZoneInfo> FindAsync(long siteId, long zoneId, CancellationToken cancellationToken)
    {
        await RequireSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        var row = await _context.Zones.AsNoTracking().Include(z => z.Resolvers).FirstOrDefaultAsync(z => z.SiteId == siteId && z.Id == zoneId, cancellationToken).ConfigureAwait(false);
        return row?.ToInfo() ?? throw NotFound(zoneId);
    }



    /// <inheritdoc/>
    public async Task<ZoneInfo> CreateAsync(long siteId, ZoneDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Validate(draft);
        await RequireSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        await RequireCodeFreeAsync(siteId, draft.Code, exceptZoneId: 0, cancellationToken).ConfigureAwait(false);
        await RequireResolversAsync(draft.Resolution, cancellationToken).ConfigureAwait(false);
        var row = new Zone { SiteId = siteId };
        row.Apply(draft, _timeProvider.GetUtcNow(), updatedBy);
        await _context.Zones.AddAsync(row, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.ToInfo();
    }



    /// <inheritdoc/>
    public async Task<ZoneInfo> UpdateAsync(long siteId, long zoneId, ZoneDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Validate(draft);
        await RequireSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        var row = await _context.Zones.Include(z => z.Resolvers).FirstOrDefaultAsync(z => z.SiteId == siteId && z.Id == zoneId, cancellationToken).ConfigureAwait(false) ?? throw NotFound(zoneId);
        await RequireCodeFreeAsync(siteId, draft.Code, zoneId, cancellationToken).ConfigureAwait(false);
        await RequireResolversAsync(draft.Resolution, cancellationToken).ConfigureAwait(false);
        if (row.IsActive && !draft.IsActive)
        {
            var open = await _openGroups.CountOpenAsync(zoneId, cancellationToken).ConfigureAwait(false);
            if (open > 0)
            {
                throw new ZoneException(ZoneErrorCodes.HasOpenGroups, $"Zone {row.Code} has {open} open zone group(s); complete them before retiring the zone.");
            }
        }

        var wanted = draft.Resolution?.ResolverUserIds ?? [];
        _context.RemoveRange(row.Resolvers.Where(r => !wanted.Contains(r.UserId)));   // the FKs are Restrict by convention: delete the orphans explicitly
        row.Apply(draft, _timeProvider.GetUtcNow(), updatedBy);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.ToInfo();
    }



    private async Task RequireSiteAsync(long siteId, CancellationToken cancellationToken)
    {
        if (!await _context.Sites.AnyAsync(s => s.Id == siteId, cancellationToken).ConfigureAwait(false))
        {
            throw new ZoneException(ZoneErrorCodes.SiteNotFound, $"Site {siteId} does not exist.");
        }
    }



    private async Task RequireCodeFreeAsync(long siteId, string code, long exceptZoneId, CancellationToken cancellationToken)
    {
        var normalized = ZoneRules.Normalize(code);
        if (await _context.Zones.AnyAsync(z => z.SiteId == siteId && z.CodeNormalized == normalized && z.Id != exceptZoneId, cancellationToken).ConfigureAwait(false))
        {
            throw new ZoneException(ZoneErrorCodes.CodeTaken, $"A zone with code '{code.Trim()}' already exists in site {siteId}.");
        }
    }



    private async Task RequireResolversAsync(ResolutionZone? resolution, CancellationToken cancellationToken)
    {
        if (resolution is null || resolution.ResolverUserIds.Count == 0)
        {
            return;
        }

        var wanted = resolution.ResolverUserIds.ToList();
        var known = await _context.Users.Where(u => wanted.Contains(u.Id)).Select(u => u.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (wanted.Except(known).FirstOrDefault() is var missing && missing != 0)
        {
            throw new ZoneException(ZoneErrorCodes.Invalid, $"resolution.resolverUserIds: user {missing} does not exist.");
        }
    }



    private static void Validate(ZoneDraft draft)
    {
        if (ZoneRules.Validate(draft) is { } reason)
        {
            throw new ZoneException(ZoneErrorCodes.Invalid, reason);
        }
    }



    private static ZoneException NotFound(long zoneId)
    {
        return new ZoneException(ZoneErrorCodes.NotFound, $"Zone {zoneId} does not exist.");
    }
}
