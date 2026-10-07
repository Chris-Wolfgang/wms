// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Http.Paging;
using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Locations;

/// <summary>
/// <see cref="ILocations"/> over <c>core.location</c> (E17.1). The list is a keyset page over one index per sort
/// (walk sequence, normalized code, barcode or id, each with the id as tiebreaker); codes and barcodes are
/// unique within the site; the zone must belong to the site and the walk sequence must start with the zone's
/// walk-order prefix; every write goes through the audited context (E6.4) and bumps the row version (E5.1).
/// </summary>
public sealed class EfLocations : ILocations
{
    private readonly WmsDbContext _context;
    private readonly TimeProvider _timeProvider;



    /// <summary>
    /// Creates the store.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfLocations(WmsDbContext context, TimeProvider timeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }



    /// <inheritdoc/>
    public async Task<Page<LocationInfo>> ListAsync(long siteId, LocationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        await RequireSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        var scope = Scoped(siteId, query);
        var total = await scope.LongCountAsync(cancellationToken).ConfigureAwait(false);
        if (total == 0)
        {
            return Page.Empty<LocationInfo>();
        }

        var page = query.Page;
        var rows = await Ordered(Seek(scope, page), page.Sort, page.Direction).Take(page.Size + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        var hasMore = rows.Count > page.Size;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        if (page.Direction == PageDirection.Backward)
        {
            rows.Reverse();   // read backwards, reported in the requested sort
        }

        var hasNext = page.Direction == PageDirection.Forward ? hasMore : !page.IsFirstPage;
        var hasPrevious = page.Direction == PageDirection.Backward ? hasMore : !page.IsFirstPage;
        var next = rows.Count > 0 && hasNext ? CursorOf(page.Sort, rows[^1]).Encode() : null;
        var previous = rows.Count > 0 && hasPrevious ? CursorOf(page.Sort, rows[0]).Encode() : null;
        var minId = await scope.MinAsync(l => l.Id, cancellationToken).ConfigureAwait(false);
        var maxId = await scope.MaxAsync(l => l.Id, cancellationToken).ConfigureAwait(false);
        return new Page<LocationInfo>(rows.Select(l => l.ToInfo()).ToList(), next, previous, total, minId, maxId);
    }



    /// <inheritdoc/>
    public async Task<LocationInfo> FindAsync(long siteId, long locationId, CancellationToken cancellationToken)
    {
        await RequireSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        var row = await _context.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.SiteId == siteId && l.Id == locationId, cancellationToken).ConfigureAwait(false);
        return row?.ToInfo() ?? throw NotFound(locationId);
    }



    /// <inheritdoc/>
    public async Task<LocationInfo> CreateAsync(long siteId, LocationDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Validate(draft);
        await RequireSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        await RequireZoneAsync(siteId, draft, cancellationToken).ConfigureAwait(false);
        await RequireFreeAsync(siteId, draft, exceptLocationId: 0, cancellationToken).ConfigureAwait(false);
        var row = new Location { SiteId = siteId };
        row.Apply(draft, _timeProvider.GetUtcNow(), updatedBy);
        await _context.Locations.AddAsync(row, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.ToInfo();
    }



    /// <inheritdoc/>
    public async Task<LocationInfo> UpdateAsync(long siteId, long locationId, LocationDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Validate(draft);
        await RequireSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        var row = await _context.Locations.FirstOrDefaultAsync(l => l.SiteId == siteId && l.Id == locationId, cancellationToken).ConfigureAwait(false) ?? throw NotFound(locationId);
        await RequireZoneAsync(siteId, draft, cancellationToken).ConfigureAwait(false);
        await RequireFreeAsync(siteId, draft, locationId, cancellationToken).ConfigureAwait(false);
        row.Apply(draft, _timeProvider.GetUtcNow(), updatedBy);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.ToInfo();
    }



    private IQueryable<Location> Scoped(long siteId, LocationQuery query)
    {
        var scope = _context.Locations.AsNoTracking().Where(l => l.SiteId == siteId);
        if (query.ZoneId is { } zoneId)
        {
            scope = scope.Where(l => l.ZoneId == zoneId);
        }

        if (query.IdFrom is { } from)
        {
            scope = scope.Where(l => l.Id >= from);
        }

        if (query.IdTo is { } to)
        {
            scope = scope.Where(l => l.Id <= to);
        }

        return scope;
    }



    /// <summary>
    /// The rows past the cursor in the paging direction: <c>(field, id)</c> strictly greater or strictly less
    /// than the cursor's pair, so a position is one pair read from one index.
    /// </summary>
    private static IQueryable<Location> Seek(IQueryable<Location> scope, PageQuery page)
    {
        if (page.IsFirstPage)
        {
            return scope;
        }

        var id = page.Cursor.Id;
        var value = page.Cursor.Value ?? string.Empty;
        var greater = page.SeeksGreater;
        return page.Sort.Field switch
        {
            LocationsModule.WalkSequenceSort => greater
                ? scope.Where(l => l.WalkSequence.CompareTo(value) > 0 || (l.WalkSequence == value && l.Id > id))
                : scope.Where(l => l.WalkSequence.CompareTo(value) < 0 || (l.WalkSequence == value && l.Id < id)),
            LocationsModule.CodeSort => greater
                ? scope.Where(l => l.CodeNormalized.CompareTo(value) > 0 || (l.CodeNormalized == value && l.Id > id))
                : scope.Where(l => l.CodeNormalized.CompareTo(value) < 0 || (l.CodeNormalized == value && l.Id < id)),
            LocationsModule.BarcodeSort => greater
                ? scope.Where(l => l.Barcode.CompareTo(value) > 0 || (l.Barcode == value && l.Id > id))
                : scope.Where(l => l.Barcode.CompareTo(value) < 0 || (l.Barcode == value && l.Id < id)),
            _ => greater ? scope.Where(l => l.Id > id) : scope.Where(l => l.Id < id),
        };
    }



    /// <summary>
    /// The requested sort with the id as tiebreaker; reversed when reading backwards so <c>Take</c> finds the
    /// rows nearest the cursor.
    /// </summary>
    private static IOrderedQueryable<Location> Ordered(IQueryable<Location> scope, SortOrder sort, PageDirection direction)
    {
        var descending = (sort.Direction == SortDirection.Descending) != (direction == PageDirection.Backward);
        return sort.Field switch
        {
            LocationsModule.WalkSequenceSort => descending ? scope.OrderByDescending(l => l.WalkSequence).ThenByDescending(l => l.Id) : scope.OrderBy(l => l.WalkSequence).ThenBy(l => l.Id),
            LocationsModule.CodeSort => descending ? scope.OrderByDescending(l => l.CodeNormalized).ThenByDescending(l => l.Id) : scope.OrderBy(l => l.CodeNormalized).ThenBy(l => l.Id),
            LocationsModule.BarcodeSort => descending ? scope.OrderByDescending(l => l.Barcode).ThenByDescending(l => l.Id) : scope.OrderBy(l => l.Barcode).ThenBy(l => l.Id),
            _ => descending ? scope.OrderByDescending(l => l.Id) : scope.OrderBy(l => l.Id),
        };
    }



    private static Cursor CursorOf(SortOrder sort, Location row)
    {
        return sort.Field switch
        {
            LocationsModule.WalkSequenceSort => Cursor.For(sort, row.WalkSequence, row.Id),
            LocationsModule.CodeSort => Cursor.For(sort, row.CodeNormalized, row.Id),
            LocationsModule.BarcodeSort => Cursor.For(sort, row.Barcode, row.Id),
            _ => Cursor.For(sort, row.Id),
        };
    }



    private async Task RequireSiteAsync(long siteId, CancellationToken cancellationToken)
    {
        if (!await _context.Sites.AnyAsync(s => s.Id == siteId, cancellationToken).ConfigureAwait(false))
        {
            throw new LocationException(LocationErrorCodes.SiteNotFound, $"Site {siteId} does not exist.");
        }
    }



    private async Task RequireZoneAsync(long siteId, LocationDraft draft, CancellationToken cancellationToken)
    {
        var prefix = await _context.Zones.AsNoTracking().Where(z => z.SiteId == siteId && z.Id == draft.ZoneId).Select(z => new { z.WalkOrderPrefix }).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new LocationException(LocationErrorCodes.ZoneNotFound, $"Zone {draft.ZoneId} is not a zone of site {siteId}.");
        if (LocationRules.WalkSequenceUnderPrefix(draft.WalkSequence, prefix.WalkOrderPrefix) is { } reason)
        {
            throw new LocationException(LocationErrorCodes.Invalid, reason);
        }
    }



    private async Task RequireFreeAsync(long siteId, LocationDraft draft, long exceptLocationId, CancellationToken cancellationToken)
    {
        var code = LocationRules.Normalize(draft.Code);
        var barcode = draft.Barcode.Trim();
        if (await _context.Locations.AnyAsync(l => l.SiteId == siteId && l.CodeNormalized == code && l.Id != exceptLocationId, cancellationToken).ConfigureAwait(false))
        {
            throw new LocationException(LocationErrorCodes.CodeTaken, $"A location with code '{draft.Code.Trim()}' already exists in site {siteId}.");
        }

        if (await _context.Locations.AnyAsync(l => l.SiteId == siteId && l.Barcode == barcode && l.Id != exceptLocationId, cancellationToken).ConfigureAwait(false))
        {
            throw new LocationException(LocationErrorCodes.BarcodeTaken, $"A location with barcode '{barcode}' already exists in site {siteId}.");
        }
    }



    private static void Validate(LocationDraft draft)
    {
        if (LocationRules.Validate(draft) is { } reason)
        {
            throw new LocationException(LocationErrorCodes.Invalid, reason);
        }
    }



    private static LocationException NotFound(long locationId)
    {
        return new LocationException(LocationErrorCodes.NotFound, $"Location {locationId} does not exist.");
    }
}
