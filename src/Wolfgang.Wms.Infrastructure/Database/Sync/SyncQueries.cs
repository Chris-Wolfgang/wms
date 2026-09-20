// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Infrastructure.Database.Conventions;

namespace Wolfgang.Wms.Infrastructure.Database.Sync;

/// <summary>
/// The two reads every synced table offers (E5.3, E5.4), written once: a delta since a watermark and a
/// manifest of live rows. Deltas read <c>WHERE row_version &gt; @since ORDER BY row_version</c> including
/// soft-deleted rows; because sequence values are assigned before commit, the watermark handed back after
/// the last page steps back by <see cref="SafetyMargin"/> versions so an out-of-order commit is re-read
/// rather than missed (clients upsert idempotently).
/// </summary>
public static class SyncQueries
{
    /// <summary>
    /// Default page size of a delta.
    /// </summary>
    public const int DefaultPageSize = 500;



    /// <summary>
    /// Largest page a delta serves.
    /// </summary>
    public const int MaxPageSize = 5000;



    /// <summary>
    /// How many versions the final watermark steps back: one sequence cache window, the most a value can be
    /// assigned ahead of a commit that is still in flight.
    /// </summary>
    public const int SafetyMargin = RowVersioning.SequenceCache;



    /// <summary>
    /// Rows changed after <paramref name="since"/>, soft-deleted ones included, in <c>row_version</c> order,
    /// projected to the API record.
    /// </summary>
    /// <typeparam name="TEntity">The synced entity.</typeparam>
    /// <typeparam name="TItem">The API record.</typeparam>
    /// <param name="source">The table (any filters already applied).</param>
    /// <param name="since">The client's watermark; 0 for everything.</param>
    /// <param name="size">Page size, clamped to 1..<see cref="MaxPageSize"/>.</param>
    /// <param name="project">Projection to the API record (client-side, after the query).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static async Task<Delta<TItem>> DeltaAsync<TEntity, TItem>(IQueryable<TEntity> source, long since, int size, Func<TEntity, TItem> project, CancellationToken cancellationToken)
        where TEntity : class, ISyncedEntity
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(project);

        var pageSize = Math.Clamp(size, 1, MaxPageSize);
        var rows = await source
            .IgnoreQueryFilters()
            .Where(e => e.RowVersion > since)
            .OrderBy(e => e.RowVersion)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = rows.Count > pageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var last = rows.Count == 0 ? since : rows[^1].RowVersion;
        return new Delta<TItem>(rows.Select(project).ToList(), NextSince(since, last, hasMore), hasMore);
    }



    /// <summary>
    /// The watermark to hand back: while more pages exist, the last version read (no step back, the client
    /// is catching up); on the final page, the last version minus <see cref="SafetyMargin"/>, but never below
    /// what the client sent, so progress is monotonic and a delayed commit is re-read.
    /// </summary>
    public static long NextSince(long since, long lastVersionRead, bool hasMore)
    {
        if (hasMore)
        {
            return Math.Max(since, lastVersionRead);
        }

        return Math.Max(since, lastVersionRead - SafetyMargin);
    }



    /// <summary>
    /// <c>(id, row_version)</c> of every live row (soft-deleted rows excluded), for reconciliation; an
    /// index-only read because both columns are indexed.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static async Task<IReadOnlyList<ManifestEntry>> ManifestAsync<TEntity>(IQueryable<TEntity> source, CancellationToken cancellationToken)
        where TEntity : class, ISyncedEntity
    {
        ArgumentNullException.ThrowIfNull(source);

        return await source
            .Where(e => e.DeletedAt == null)
            .OrderBy(e => e.Id)
            .Select(e => new ManifestEntry(e.Id, e.RowVersion))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
