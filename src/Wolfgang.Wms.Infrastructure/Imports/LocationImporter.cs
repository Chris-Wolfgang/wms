// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Imports;
using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Core.Zones;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Locations;

namespace Wolfgang.Wms.Infrastructure.Imports;

/// <summary>
/// The locations file (E16.6): an idempotent upsert by bin code within the site. Plans every row against the
/// site's zones (named by code) and current bins (barcodes must stay unique, the walk sequence must start with
/// the zone's prefix), then writes the planned rows in one transaction.
/// </summary>
internal sealed class LocationImporter
{
    private const string Entity = "locations";
    private readonly WmsDbContext _context;
    private readonly TimeProvider _timeProvider;



    public LocationImporter(WmsDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }



    public async Task<ImportResult> RunAsync(long siteId, IReadOnlyList<LocationImportRow> rows, ImportPolicy policy, string updatedBy, CancellationToken cancellationToken)
    {
        var zones = await _context.Zones.AsNoTracking().Where(z => z.SiteId == siteId).Select(z => new ZoneRef(z.Id, z.CodeNormalized, z.WalkOrderPrefix)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var existing = await _context.Locations.Where(l => l.SiteId == siteId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var site = new SiteRows(zones.ToDictionary(z => z.CodeNormalized, StringComparer.Ordinal), existing.ToDictionary(l => l.CodeNormalized, StringComparer.Ordinal), existing.ToDictionary(l => l.Barcode, StringComparer.Ordinal));
        var plan = new List<Planned>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            plan.Add(Decide(i + 1, rows[i], site));
        }

        var results = plan.Select(p => p.Result).ToList();
        var written = ImportRules.ShouldWrite(policy, results);
        if (written)
        {
            await ApplyAsync(siteId, plan, updatedBy, cancellationToken).ConfigureAwait(false);
        }

        return ImportResult.From(Entity, policy, written, results);
    }



    private static Planned Decide(int number, LocationImportRow row, SiteRows site)
    {
        var key = (row.Code ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(key))
        {
            return Planned.Failed(number, key, ImportErrorCodes.Invalid, "code is required.");
        }

        var normalized = LocationRules.Normalize(key);
        if (!site.SeenCodes.Add(normalized))
        {
            return Planned.Failed(number, key, ImportErrorCodes.DuplicateInFile, $"Location '{key}' appears earlier in the file.");
        }

        site.ByCode.TryGetValue(normalized, out var current);
        return row.Action == ImportAction.Delete ? DecideDelete(number, key, current) : DecideUpsert(number, key, row, current, site);
    }



    private static Planned DecideDelete(int number, string key, Location? current)
    {
        if (current is null)
        {
            return Planned.Failed(number, key, ImportErrorCodes.KeyNotFound, $"Location '{key}' does not exist; nothing to delete.");
        }

        var outcome = current.IsActive ? ImportRowOutcome.Deleted : ImportRowOutcome.Unchanged;
        return new Planned(new ImportRowResult(number, key, outcome, Code: null, Message: null), current, Draft: null);
    }



    private static Planned DecideUpsert(int number, string key, LocationImportRow row, Location? current, SiteRows site)
    {
        var zoneCode = (row.ZoneCode ?? string.Empty).Trim();
        if (zoneCode.Length == 0 || !site.Zones.TryGetValue(ZoneRules.Normalize(zoneCode), out var zone))
        {
            return Planned.Failed(number, key, ImportErrorCodes.ReferenceNotFound, $"zoneCode '{zoneCode}' is not a zone of the site; load the zones file first.");
        }

        var draft = row.ToDraft(zone.Id);
        if ((LocationRules.Validate(draft) ?? LocationRules.WalkSequenceUnderPrefix(draft.WalkSequence, zone.WalkOrderPrefix)) is { } reason)
        {
            return Planned.Failed(number, key, LocationErrorCodes.Invalid, reason);
        }

        var barcode = draft.Barcode.Trim();
        if (!site.SeenBarcodes.Add(barcode))
        {
            return Planned.Failed(number, key, ImportErrorCodes.DuplicateInFile, $"Barcode '{barcode}' appears earlier in the file.");
        }

        if (site.ByBarcode.TryGetValue(barcode, out var holder) && !string.Equals(holder.CodeNormalized, LocationRules.Normalize(key), StringComparison.Ordinal))
        {
            return Planned.Failed(number, key, LocationErrorCodes.BarcodeTaken, $"Barcode '{barcode}' belongs to location '{holder.Code}'; relabel it in its own row first.");
        }

        if (current is null)
        {
            return new Planned(new ImportRowResult(number, key, ImportRowOutcome.Inserted, Code: null, Message: null), Target: null, draft);
        }

        var outcome = current.Matches(draft) ? ImportRowOutcome.Unchanged : ImportRowOutcome.Updated;
        return new Planned(new ImportRowResult(number, key, outcome, Code: null, Message: null), current, outcome == ImportRowOutcome.Updated ? draft : null);
    }



    private Task ApplyAsync(long siteId, List<Planned> plan, string updatedBy, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var inserted = new List<Location>();
        foreach (var planned in plan)
        {
            switch (planned.Result.Outcome)
            {
                case ImportRowOutcome.Inserted:
                    var row = new Location { SiteId = siteId };
                    row.Apply(planned.Draft!, now, updatedBy);
                    inserted.Add(row);
                    break;
                case ImportRowOutcome.Updated:
                    planned.Target!.Apply(planned.Draft!, now, updatedBy);
                    break;
                case ImportRowOutcome.Deleted:
                    planned.Target!.IsActive = false;
                    planned.Target.UpdatedAt = now;
                    planned.Target.UpdatedBy = updatedBy;
                    break;
                default:
                    break;
            }
        }

        // The providers retry transient failures, so a user transaction must run inside the execution strategy;
        // the tracked changes above are the same on every attempt.
        return _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await _context.Locations.AddRangeAsync(inserted, cancellationToken).ConfigureAwait(false);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        });
    }




    private sealed record ZoneRef(long Id, string CodeNormalized, string? WalkOrderPrefix);



    private sealed record SiteRows(Dictionary<string, ZoneRef> Zones, Dictionary<string, Location> ByCode, Dictionary<string, Location> ByBarcode)
    {
        public HashSet<string> SeenCodes { get; } = new(StringComparer.Ordinal);

        public HashSet<string> SeenBarcodes { get; } = new(StringComparer.Ordinal);
    }



    private sealed record Planned(ImportRowResult Result, Location? Target, LocationDraft? Draft)
    {
        public static Planned Failed(int number, string key, Domain.Keys.ErrorCode code, string message)
        {
            return new Planned(new ImportRowResult(number, key, ImportRowOutcome.Failed, code.Code, message), Target: null, Draft: null);
        }
    }
}
