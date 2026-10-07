// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Sites;

/// <summary>
/// <see cref="ISites"/> over <c>core.site</c> (E16.1). Codes are unique without regard to case; deactivating a
/// site asks <see cref="IOpenReleases"/> first; every write goes through the audited context (E6.4) and bumps
/// the row version (E5.1).
/// </summary>
public sealed class EfSites : ISites
{
    private readonly WmsDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IOpenReleases _openReleases;



    /// <summary>
    /// Creates the store.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfSites(WmsDbContext context, TimeProvider timeProvider, IOpenReleases openReleases)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _openReleases = openReleases ?? throw new ArgumentNullException(nameof(openReleases));
    }



    /// <inheritdoc/>
    public async Task<IReadOnlyList<SiteInfo>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await _context.Sites.AsNoTracking().OrderBy(s => s.CodeNormalized).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(s => s.ToInfo()).ToList();
    }



    /// <inheritdoc/>
    public async Task<SiteInfo> FindAsync(long siteId, CancellationToken cancellationToken)
    {
        var row = await _context.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == siteId, cancellationToken).ConfigureAwait(false);
        return row?.ToInfo() ?? throw NotFound(siteId);
    }



    /// <inheritdoc/>
    public async Task<SiteInfo> CreateAsync(SiteDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Validate(draft);
        await RequireCodeFreeAsync(draft.Code, exceptSiteId: 0, cancellationToken).ConfigureAwait(false);
        var row = new Site();
        row.Apply(draft, _timeProvider.GetUtcNow(), updatedBy);
        await _context.Sites.AddAsync(row, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.ToInfo();
    }



    /// <inheritdoc/>
    public async Task<SiteInfo> UpdateAsync(long siteId, SiteDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Validate(draft);
        var row = await _context.Sites.FirstOrDefaultAsync(s => s.Id == siteId, cancellationToken).ConfigureAwait(false) ?? throw NotFound(siteId);
        await RequireCodeFreeAsync(draft.Code, siteId, cancellationToken).ConfigureAwait(false);
        if (row.IsActive && !draft.IsActive)
        {
            var open = await _openReleases.CountOpenAsync(siteId, cancellationToken).ConfigureAwait(false);
            if (open > 0)
            {
                throw new SiteException(SiteErrorCodes.HasOpenReleases, $"Site {row.Code} has {open} open release(s); complete or cancel them before retiring the site.");
            }
        }

        row.Apply(draft, _timeProvider.GetUtcNow(), updatedBy);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.ToInfo();
    }



    private async Task RequireCodeFreeAsync(string code, long exceptSiteId, CancellationToken cancellationToken)
    {
        var normalized = SiteRules.Normalize(code);
        if (await _context.Sites.AnyAsync(s => s.CodeNormalized == normalized && s.Id != exceptSiteId, cancellationToken).ConfigureAwait(false))
        {
            throw new SiteException(SiteErrorCodes.CodeTaken, $"A site with code '{code.Trim()}' already exists.");
        }
    }



    private static void Validate(SiteDraft draft)
    {
        if (SiteRules.Validate(draft) is { } reason)
        {
            throw new SiteException(SiteErrorCodes.Invalid, reason);
        }
    }



    private static SiteException NotFound(long siteId)
    {
        return new SiteException(SiteErrorCodes.NotFound, $"Site {siteId} does not exist.");
    }
}
