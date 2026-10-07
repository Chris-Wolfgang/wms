// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Copies;
using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Core.Zones;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Locations;
using Wolfgang.Wms.Infrastructure.Sites;
using Wolfgang.Wms.Infrastructure.Zones;

namespace Wolfgang.Wms.Infrastructure.Copies;

/// <summary>
/// <see cref="ICopies"/> over the stored master data (E16.5). Each copy is planned against the current rows
/// (codes and barcodes must be free in the target), written in one transaction inside the provider's execution
/// strategy, and then given its settings: the new scopes are populated (E16.4) and the source's overrides
/// re-applied, secrets excepted. Every new row carries its <c>CopiedFromId</c>, which the audit records.
/// </summary>
public sealed class EfCopies : ICopies
{
    private readonly WmsDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ISettings _settings;
    private readonly SettingRegistry _registry;



    /// <summary>
    /// Creates the copier.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfCopies(WmsDbContext context, TimeProvider timeProvider, ISettings settings, SettingRegistry registry)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }



    /// <inheritdoc/>
    public async Task<SiteInfo> CopySiteAsync(long siteId, SiteCopyRequest request, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Require(CopyRules.ValidateSubstitution("codePrefixFrom", request.CodePrefixFrom, "codePrefixTo", request.CodePrefixTo));
        var source = await _context.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == siteId, cancellationToken).ConfigureAwait(false)
            ?? throw new CopyException(CopyErrorCodes.NotFound, $"Site {siteId} does not exist.");
        var draft = new SiteDraft(request.Code, request.Name, string.IsNullOrWhiteSpace(request.TimeZone) ? source.TimeZone : request.TimeZone);
        Require(SiteRules.Validate(draft));
        if (await _context.Sites.AnyAsync(s => s.CodeNormalized == SiteRules.Normalize(draft.Code), cancellationToken).ConfigureAwait(false))
        {
            throw new CopyException(CopyErrorCodes.CodeTaken, $"A site with code '{draft.Code.Trim()}' already exists.");
        }

        var zones = request.Zones ? await _context.Zones.AsNoTracking().Include(z => z.Resolvers).Where(z => z.SiteId == siteId).OrderBy(z => z.Id).ToListAsync(cancellationToken).ConfigureAwait(false) : [];
        var locations = request.Zones && request.Locations ? await _context.Locations.AsNoTracking().Where(l => l.SiteId == siteId).OrderBy(l => l.Id).ToListAsync(cancellationToken).ConfigureAwait(false) : [];
        var now = _timeProvider.GetUtcNow();
        var site = new Site { CopiedFromId = source.Id };
        site.Apply(draft, now, updatedBy);
        var zoneCopies = zones.Select(z => (Source: z, Copy: CloneZone(z, z.Code, now, updatedBy))).ToList();
        var locationCopies = locations.Select(l => CloneLocation(l, CopyRules.Substitute(l.Code, request.CodePrefixFrom, request.CodePrefixTo), CopyRules.Substitute(l.Barcode, request.CodePrefixFrom, request.CodePrefixTo), l.WalkSequence, now, updatedBy)).ToList();
        RequireDistinct(locationCopies);

        await WriteAsync(site, zoneCopies, locationCopies, cancellationToken).ConfigureAwait(false);
        await _settings.PopulateAsync(SettingScopeRef.Site(site.Id), updatedBy, cancellationToken).ConfigureAwait(false);
        if (request.Settings)
        {
            await CopySettingsAsync(SettingScopeRef.Site(source.Id), SettingScopeRef.Site(site.Id), updatedBy, cancellationToken).ConfigureAwait(false);
        }

        foreach (var (sourceZone, copy) in zoneCopies)
        {
            await _settings.PopulateAsync(SettingScopeRef.Zone(copy.Id), updatedBy, cancellationToken).ConfigureAwait(false);
            if (request.Settings)
            {
                await CopySettingsAsync(SettingScopeRef.Zone(sourceZone.Id), SettingScopeRef.Zone(copy.Id), updatedBy, cancellationToken).ConfigureAwait(false);
            }
        }

        return site.ToInfo();
    }



    /// <inheritdoc/>
    public async Task<ZoneInfo> CopyZoneAsync(long siteId, long zoneId, ZoneCopyRequest request, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Require(CopyRules.ValidateSubstitution("codePrefixFrom", request.CodePrefixFrom, "codePrefixTo", request.CodePrefixTo));
        var source = await _context.Zones.AsNoTracking().Include(z => z.Resolvers).FirstOrDefaultAsync(z => z.SiteId == siteId && z.Id == zoneId, cancellationToken).ConfigureAwait(false)
            ?? throw new CopyException(CopyErrorCodes.NotFound, $"Zone {zoneId} does not exist in site {siteId}.");
        var targetSiteId = request.TargetSiteId ?? siteId;
        if (!await _context.Sites.AnyAsync(s => s.Id == targetSiteId, cancellationToken).ConfigureAwait(false))
        {
            throw new CopyException(CopyErrorCodes.NotFound, $"Target site {targetSiteId} does not exist.");
        }

        var copy = CloneZone(source, request.Code, _timeProvider.GetUtcNow(), updatedBy);
        copy.Name = (request.Name ?? string.Empty).Trim();
        copy.SiteId = targetSiteId;
        Require(ZoneRules.Validate(new ZoneDraft(copy.Code, copy.Name, copy.Type, copy.WalkOrderPrefix, copy.IsRejectLane, source.ToInfo().Resolution, copy.IsActive)));
        if (await _context.Zones.AnyAsync(z => z.SiteId == targetSiteId && z.CodeNormalized == copy.CodeNormalized, cancellationToken).ConfigureAwait(false))
        {
            throw new CopyException(CopyErrorCodes.CodeTaken, $"A zone with code '{copy.Code}' already exists in site {targetSiteId}.");
        }

        var locationCopies = await PlanZoneLocationsAsync(source, request, targetSiteId, updatedBy, cancellationToken).ConfigureAwait(false);
        await WriteAsync(site: null, [(source, copy)], locationCopies, cancellationToken).ConfigureAwait(false);
        await _settings.PopulateAsync(SettingScopeRef.Zone(copy.Id), updatedBy, cancellationToken).ConfigureAwait(false);
        if (request.Settings)
        {
            await CopySettingsAsync(SettingScopeRef.Zone(source.Id), SettingScopeRef.Zone(copy.Id), updatedBy, cancellationToken).ConfigureAwait(false);
        }

        return copy.ToInfo();
    }



    /// <inheritdoc/>
    public async Task<LocationInfo> CopyLocationAsync(long siteId, long locationId, LocationCopyRequest request, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        var source = await _context.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.SiteId == siteId && l.Id == locationId, cancellationToken).ConfigureAwait(false)
            ?? throw new CopyException(CopyErrorCodes.NotFound, $"Location {locationId} does not exist in site {siteId}.");
        var copy = CloneLocation(source, request.Code ?? string.Empty, request.Barcode ?? string.Empty, string.IsNullOrWhiteSpace(request.WalkSequence) ? source.WalkSequence : request.WalkSequence, _timeProvider.GetUtcNow(), updatedBy);
        copy.ZoneId = request.ZoneId ?? source.ZoneId;
        await RequireLocationsAsync(siteId, [copy], cancellationToken).ConfigureAwait(false);
        await WriteAsync(site: null, [], [copy], cancellationToken).ConfigureAwait(false);
        return copy.ToInfo();
    }



    /// <inheritdoc/>
    public async Task<IReadOnlyList<LocationInfo>> CopyLocationRangeAsync(long siteId, LocationRangeCopyRequest request, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Require(CopyRules.ValidateSubstitution("codePrefixFrom", request.CodePrefixFrom, "codePrefixTo", request.CodePrefixTo));
        Require(string.IsNullOrWhiteSpace(request.CodePrefixFrom) ? "codePrefixFrom is required." : null);
        Require(CopyRules.ValidateSubstitution("walkPrefixFrom", request.WalkPrefixFrom, "walkPrefixTo", request.WalkPrefixTo));
        if (!await _context.Sites.AnyAsync(s => s.Id == siteId, cancellationToken).ConfigureAwait(false))
        {
            throw new CopyException(CopyErrorCodes.NotFound, $"Site {siteId} does not exist.");
        }

        var prefix = LocationRules.Normalize(request.CodePrefixFrom);
        var sources = await _context.Locations.AsNoTracking().Where(l => l.SiteId == siteId && l.CodeNormalized.StartsWith(prefix)).OrderBy(l => l.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (sources.Count == 0)
        {
            throw new CopyException(CopyErrorCodes.NotFound, $"No location of site {siteId} has a code starting with '{request.CodePrefixFrom.Trim()}'.");
        }

        var now = _timeProvider.GetUtcNow();
        var copies = sources.Select(l => CloneLocation(l, CopyRules.Substitute(l.Code, request.CodePrefixFrom, request.CodePrefixTo), CopyRules.Substitute(l.Barcode, request.CodePrefixFrom, request.CodePrefixTo), CopyRules.Substitute(l.WalkSequence, request.WalkPrefixFrom, request.WalkPrefixTo), now, updatedBy)).ToList();
        foreach (var copy in copies.Where(_ => request.ZoneId is not null))
        {
            copy.ZoneId = request.ZoneId!.Value;
        }

        await RequireLocationsAsync(siteId, copies, cancellationToken).ConfigureAwait(false);
        await WriteAsync(site: null, [], copies, cancellationToken).ConfigureAwait(false);
        return copies.Select(c => c.ToInfo()).ToList();
    }



    private async Task<List<Location>> PlanZoneLocationsAsync(Zone source, ZoneCopyRequest request, long targetSiteId, string updatedBy, CancellationToken cancellationToken)
    {
        if (!request.Locations)
        {
            return [];
        }

        if (targetSiteId == source.SiteId && string.IsNullOrWhiteSpace(request.CodePrefixFrom))
        {
            throw new CopyException(CopyErrorCodes.Invalid, "Copying a zone's locations within the same site needs codePrefixFrom and codePrefixTo, so the copies get codes and barcodes of their own.");
        }

        var sources = await _context.Locations.AsNoTracking().Where(l => l.ZoneId == source.Id).OrderBy(l => l.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        var copies = sources.Select(l => CloneLocation(l, CopyRules.Substitute(l.Code, request.CodePrefixFrom, request.CodePrefixTo), CopyRules.Substitute(l.Barcode, request.CodePrefixFrom, request.CodePrefixTo), l.WalkSequence, now, updatedBy)).ToList();
        foreach (var copy in copies)
        {
            copy.SiteId = targetSiteId;
        }

        await RequireLocationsAsync(targetSiteId, copies, cancellationToken).ConfigureAwait(false);
        return copies;
    }



    /// <summary>
    /// The planned locations are valid, distinct among themselves, and their codes and barcodes are free in the
    /// site; the zone they go to belongs to the site and its walk-order prefix holds.
    /// </summary>
    /// <exception cref="CopyException">A zone is missing, a copy is invalid, or a code or barcode is taken.</exception>
    private async Task RequireLocationsAsync(long siteId, List<Location> copies, CancellationToken cancellationToken)
    {
        RequireDistinct(copies);
        var zoneIds = copies.Select(c => c.ZoneId).Distinct().ToList();
        var zones = await _context.Zones.AsNoTracking().Where(z => z.SiteId == siteId && zoneIds.Contains(z.Id)).Select(z => new { z.Id, z.WalkOrderPrefix }).ToDictionaryAsync(z => z.Id, cancellationToken).ConfigureAwait(false);
        foreach (var copy in copies)
        {
            if (!zones.TryGetValue(copy.ZoneId, out var zone))
            {
                throw new CopyException(CopyErrorCodes.NotFound, $"Zone {copy.ZoneId} does not exist in site {siteId}.");
            }

            var draft = new LocationDraft(copy.Code, copy.Barcode, copy.ZoneId, copy.WalkSequence, copy.IsPickable, copy.IsActive);
            Require(LocationRules.Validate(draft) ?? LocationRules.WalkSequenceUnderPrefix(copy.WalkSequence, zone.WalkOrderPrefix));
        }

        var codes = copies.Select(c => c.CodeNormalized).ToList();
        var barcodes = copies.Select(c => c.Barcode).ToList();
        var takenCode = await _context.Locations.Where(l => l.SiteId == siteId && codes.Contains(l.CodeNormalized)).Select(l => l.Code).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (takenCode is not null)
        {
            throw new CopyException(CopyErrorCodes.CodeTaken, $"A location with code '{takenCode}' already exists in site {siteId}.");
        }

        var takenBarcode = await _context.Locations.Where(l => l.SiteId == siteId && barcodes.Contains(l.Barcode)).Select(l => l.Barcode).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (takenBarcode is not null)
        {
            throw new CopyException(CopyErrorCodes.BarcodeTaken, $"A location with barcode '{takenBarcode}' already exists in site {siteId}.");
        }
    }



    private static void RequireDistinct(List<Location> copies)
    {
        var code = copies.GroupBy(c => c.CodeNormalized, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (code is not null)
        {
            throw new CopyException(CopyErrorCodes.CodeTaken, $"The substitution gives two copies the code '{code.Key}'.");
        }

        var barcode = copies.GroupBy(c => c.Barcode, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (barcode is not null)
        {
            throw new CopyException(CopyErrorCodes.BarcodeTaken, $"The substitution gives two copies the barcode '{barcode.Key}'.");
        }
    }



    /// <summary>
    /// Writes the site (if any), then its zones, then the locations, in one transaction inside the execution
    /// strategy; the tracker is cleared at the start of each attempt so a retry inserts nothing twice.
    /// </summary>
    private Task WriteAsync(Site? site, List<(Zone Source, Zone Copy)> zones, List<Location> locations, CancellationToken cancellationToken)
    {
        return _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                if (site is not null)
                {
                    await _context.Sites.AddAsync(site, cancellationToken).ConfigureAwait(false);
                    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                foreach (var (_, copy) in zones.Where(_ => site is not null))
                {
                    copy.SiteId = site!.Id;
                }

                await _context.Zones.AddRangeAsync(zones.Select(z => z.Copy), cancellationToken).ConfigureAwait(false);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                foreach (var location in locations)
                {
                    var zone = zones.FirstOrDefault(z => z.Source.Id == location.ZoneId);
                    location.ZoneId = zone.Copy?.Id ?? location.ZoneId;
                    location.SiteId = site?.Id ?? location.SiteId;
                }

                await _context.Locations.AddRangeAsync(locations, cancellationToken).ConfigureAwait(false);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        });
    }



    /// <summary>
    /// Re-applies the source scope's configured values and cascade modes on the copy; secrets are never copied.
    /// </summary>
    private async Task CopySettingsAsync(SettingScopeRef source, SettingScopeRef target, string updatedBy, CancellationToken cancellationToken)
    {
        foreach (var value in await _settings.ListAsync(source, cancellationToken).ConfigureAwait(false))
        {
            if (value.Kind == SettingKind.Secret)
            {
                continue;
            }

            if (value.ConfiguredValue is not null)
            {
                await _settings.SetTextAsync(value.Name, target, value.ConfiguredValue, updatedBy, cancellationToken).ConfigureAwait(false);
            }

            if (CascadeModeExtensions.TryParseMode(value.CascadeMode, out var mode) && mode != CascadeMode.Value && _registry.TryGet(value.Name, out var key))
            {
                await _settings.SetModeAsync(key, target, mode, updatedBy, cancellationToken).ConfigureAwait(false);
            }
        }
    }



    private static Zone CloneZone(Zone source, string code, DateTimeOffset now, string updatedBy)
    {
        var copy = new Zone { SiteId = source.SiteId, CopiedFromId = source.Id };
        copy.Apply(new ZoneDraft(code, source.Name, source.Type, source.WalkOrderPrefix, source.IsRejectLane, source.ToInfo().Resolution, source.IsActive), now, updatedBy);
        return copy;
    }



    private static Location CloneLocation(Location source, string code, string barcode, string walkSequence, DateTimeOffset now, string updatedBy)
    {
        var copy = new Location { SiteId = source.SiteId, CopiedFromId = source.Id };
        copy.Apply(new LocationDraft(code, barcode, source.ZoneId, walkSequence, source.IsPickable, source.IsActive), now, updatedBy);
        return copy;
    }



    private static void Require(string? reason)
    {
        if (reason is not null)
        {
            throw new CopyException(CopyErrorCodes.Invalid, reason);
        }
    }
}
