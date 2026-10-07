// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Sites;

/// <summary>
/// <see cref="ISites"/> over <c>layout.site</c> (E16.1). Codes are unique without regard to case; a draft without
/// a time zone takes the organisation's default (E16.4); a new site's settings scope is populated with the
/// organisation's effective values at once (E7.3, E16.4); deactivating a site asks <see cref="IOpenReleases"/>
/// first; every write goes through the audited context (E6.4) and bumps the row version (E5.1).
/// </summary>
public sealed class EfSites : ISites
{
    private readonly WmsDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IOpenReleases _openReleases;
    private readonly ISettings _settings;



    /// <summary>
    /// Creates the store.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfSites(WmsDbContext context, TimeProvider timeProvider, IOpenReleases openReleases, ISettings settings)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _openReleases = openReleases ?? throw new ArgumentNullException(nameof(openReleases));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }



    /// <inheritdoc/>
    public async Task<IReadOnlyList<SiteInfo>> ListAsync(SiteScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var rows = await _context.Sites.AsNoTracking().InScope(scope, nameof(Site.Id)).OrderBy(s => s.CodeNormalized).ToListAsync(cancellationToken).ConfigureAwait(false);   // E16.3: a site-scoped reader sees their sites only
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

        draft = await WithDefaultsAsync(draft, cancellationToken).ConfigureAwait(false);
        Validate(draft);
        await RequireCodeFreeAsync(draft.Code, exceptSiteId: 0, cancellationToken).ConfigureAwait(false);
        var row = new Site();
        row.Apply(draft, _timeProvider.GetUtcNow(), updatedBy);
        await _context.Sites.AddAsync(row, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _settings.PopulateAsync(SettingScopeRef.Site(row.Id), updatedBy, cancellationToken).ConfigureAwait(false);   // E16.4: nothing beneath the new site is ever unresolved
        return row.ToInfo();
    }



    /// <inheritdoc/>
    public async Task<SiteInfo> UpdateAsync(long siteId, SiteDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        draft = await WithDefaultsAsync(draft, cancellationToken).ConfigureAwait(false);
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



    /// <summary>
    /// E16.4: a draft without a time zone takes the organisation's default; without an organisation the rules
    /// then report the missing time zone.
    /// </summary>
    private async Task<SiteDraft> WithDefaultsAsync(SiteDraft draft, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(draft.TimeZone))
        {
            return draft;
        }

        var organizationTimeZone = await _context.Organizations.AsNoTracking().OrderBy(o => o.Id).Select(o => o.TimeZone).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return organizationTimeZone is null ? draft : draft with { TimeZone = organizationTimeZone };
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
