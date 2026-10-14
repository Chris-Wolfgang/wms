// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Imports;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Core.Zones;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Zones;

namespace Wolfgang.Wms.Infrastructure.Imports;

/// <summary>
/// The zones file (E16.6): an idempotent upsert by code within the site. Plans every row against the site's
/// current zones (resolution zones are never importable nor overwritten; a delete or a deactivation asks
/// <see cref="IOpenZoneGroups"/>), then writes the planned rows in one transaction and populates the settings
/// scope of every new zone (E16.4).
/// </summary>
internal sealed class ZoneImporter
{
    private const string Entity = "zones";
    private readonly WmsDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ISettings _settings;
    private readonly IOpenZoneGroups _openGroups;



    public ZoneImporter(WmsDbContext context, TimeProvider timeProvider, ISettings settings, IOpenZoneGroups openGroups)
    {
        _context = context;
        _timeProvider = timeProvider;
        _settings = settings;
        _openGroups = openGroups;
    }



    public async Task<ImportResult> RunAsync(long siteId, IReadOnlyList<ZoneImportRow> rows, ImportPolicy policy, string updatedBy, CancellationToken cancellationToken)
    {
        var existing = await _context.Zones.Include(z => z.Resolvers).Where(z => z.SiteId == siteId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var byCode = existing.ToDictionary(z => z.CodeNormalized, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var plan = new List<Planned>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            plan.Add(await DecideAsync(i + 1, rows[i], byCode, seen, cancellationToken).ConfigureAwait(false));
        }

        var results = plan.Select(p => p.Result).ToList();
        var written = ImportRules.ShouldWrite(policy, results);
        if (written)
        {
            await ApplyAsync(siteId, plan, updatedBy, cancellationToken).ConfigureAwait(false);
        }

        return ImportResult.From(Entity, policy, written, results);
    }



    private async Task<Planned> DecideAsync(int number, ZoneImportRow row, Dictionary<string, Zone> byCode, HashSet<string> seen, CancellationToken cancellationToken)
    {
        var key = (row.Code ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(key))
        {
            return Planned.Failed(number, key, ImportErrorCodes.Invalid, "code is required.");
        }

        var normalized = ZoneRules.Normalize(key);
        if (!seen.Add(normalized))
        {
            return Planned.Failed(number, key, ImportErrorCodes.DuplicateInFile, $"Zone '{key}' appears earlier in the file.");
        }

        byCode.TryGetValue(normalized, out var current);
        if (current is not null && current.Type == ZoneType.Resolution)
        {
            return Planned.Failed(number, key, ImportErrorCodes.ResolutionZone, $"Zone '{key}' is a resolution zone; resolution zones are managed in the console or the API, not by import.");
        }

        return row.Action == ImportAction.Delete
            ? await DecideDeleteAsync(number, key, current, cancellationToken).ConfigureAwait(false)
            : await DecideUpsertAsync(number, key, row, current, cancellationToken).ConfigureAwait(false);
    }



    private async Task<Planned> DecideDeleteAsync(int number, string key, Zone? current, CancellationToken cancellationToken)
    {
        if (current is null)
        {
            return Planned.Failed(number, key, ImportErrorCodes.KeyNotFound, $"Zone '{key}' does not exist; nothing to delete.");
        }

        if (!current.IsActive)
        {
            return new Planned(new ImportRowResult(number, key, ImportRowOutcome.Unchanged, Code: null, Message: null), current, Draft: null);
        }

        return await OpenGroupsAsync(current, cancellationToken).ConfigureAwait(false) is { } blocked
            ? Planned.Failed(number, key, ZoneErrorCodes.HasOpenGroups, blocked)
            : new Planned(new ImportRowResult(number, key, ImportRowOutcome.Deleted, Code: null, Message: null), current, Draft: null);
    }



    private async Task<Planned> DecideUpsertAsync(int number, string key, ZoneImportRow row, Zone? current, CancellationToken cancellationToken)
    {
        if (row.Type == ZoneType.Resolution)
        {
            return Planned.Failed(number, key, ImportErrorCodes.ResolutionZone, "Resolution zones are created in the console or the API, not by import.");
        }

        var draft = row.ToDraft();
        if (ZoneRules.Validate(draft) is { } reason)
        {
            return Planned.Failed(number, key, ZoneErrorCodes.Invalid, reason);
        }

        if (current is null)
        {
            return new Planned(new ImportRowResult(number, key, ImportRowOutcome.Inserted, Code: null, Message: null), Target: null, draft);
        }

        if (current.Matches(draft))
        {
            return new Planned(new ImportRowResult(number, key, ImportRowOutcome.Unchanged, Code: null, Message: null), current, Draft: null);
        }

        if (current.IsActive && !draft.IsActive && await OpenGroupsAsync(current, cancellationToken).ConfigureAwait(false) is { } blocked)
        {
            return Planned.Failed(number, key, ZoneErrorCodes.HasOpenGroups, blocked);
        }

        return new Planned(new ImportRowResult(number, key, ImportRowOutcome.Updated, Code: null, Message: null), current, draft);
    }



    private async Task<string?> OpenGroupsAsync(Zone zone, CancellationToken cancellationToken)
    {
        var open = await _openGroups.CountOpenAsync(zone.Id, cancellationToken).ConfigureAwait(false);
        return open > 0 ? $"Zone {zone.Code} has {open} open zone group(s); complete them before retiring the zone." : null;
    }



    private async Task ApplyAsync(long siteId, List<Planned> plan, string updatedBy, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var inserted = new List<Zone>();
        foreach (var planned in plan)
        {
            switch (planned.Result.Outcome)
            {
                case ImportRowOutcome.Inserted:
                    var row = new Zone { SiteId = siteId };
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
        await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await _context.Zones.AddRangeAsync(inserted, cancellationToken).ConfigureAwait(false);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);

        foreach (var zone in inserted)
        {
            await _settings.PopulateAsync(SettingScopeRef.Zone(zone.Id), updatedBy, cancellationToken).ConfigureAwait(false);   // E16.4: a new zone starts with its site's effective values
        }
    }




    private sealed record Planned(ImportRowResult Result, Zone? Target, ZoneDraft? Draft)
    {
        public static Planned Failed(int number, string key, Domain.Keys.ErrorCode code, string message)
        {
            return new Planned(new ImportRowResult(number, key, ImportRowOutcome.Failed, code.Code, message), Target: null, Draft: null);
        }
    }
}
