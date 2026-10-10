// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Caching;

/// <summary>
/// A per-instance in-memory cache of one value invalidated by row version (E1.12, ADR 0003). The value is
/// reloaded only when the <see cref="RowVersionStamp"/> of the entity types it was built from has changed
/// (a higher <c>row_version</c> for an insert or update, a different row count for a delete), and that stamp
/// is probed at most once per <see cref="PollInterval"/>, so a hot read costs nothing between polls and at
/// most one cheap stamp query otherwise. There is no shared cache component: every process holds its own
/// copy and the database is the source of truth.
/// </summary>
/// <typeparam name="TValue">The cached value; a read model built from the watched entity types.</typeparam>
public sealed class VersionedCache<TValue> : IDisposable
    where TValue : class
{
    private readonly IRowVersionSource _source;
    private readonly IReadOnlyCollection<Type> _entityTypes;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Entry? _entry;
    private int _generation;   // bumped by Invalidate; a read publishes nothing it learned under an older generation



    /// <summary>
    /// Creates a cache over the tables of <paramref name="entityTypes"/> that re-checks their stamp at most
    /// once per <paramref name="pollInterval"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="entityTypes"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pollInterval"/> is negative.</exception>
    public VersionedCache
    (
        IRowVersionSource source,
        IReadOnlyCollection<Type> entityTypes,
        TimeSpan pollInterval,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entityTypes);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (pollInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), pollInterval, "The poll interval cannot be negative.");
        }

        if (entityTypes.Count == 0)
        {
            throw new ArgumentException("A versioned cache must watch at least one entity type.", nameof(entityTypes));
        }

        _source = source;
        _entityTypes = entityTypes;
        PollInterval = pollInterval;
        _timeProvider = timeProvider;
    }



    /// <summary>
    /// The minimum time between two row-version probes. Reads inside the interval are served from the copy.
    /// </summary>
    public TimeSpan PollInterval { get; }



    /// <summary>
    /// The stamp the cached value was built at, or null when nothing is cached.
    /// </summary>
    public RowVersionStamp? CachedStamp => _entry?.Stamp;



    /// <summary>
    /// The cached value, reloaded through <paramref name="load"/> when the watched tables' stamp has changed
    /// since it was built. Concurrent callers share one load.
    /// </summary>
    /// <param name="load">Builds the value from the database; receives the cancellation token.</param>
    /// <param name="cancellationToken">Cancels the stamp probe or the load.</param>
    /// <remarks>
    /// An <see cref="Invalidate"/> that lands while a read is probing or loading is honoured: the read still
    /// returns what it has, but publishes nothing, so the next read probes and reloads. Without that, a probe
    /// that started before a writer's commit could republish the pre-commit copy over the invalidation.
    /// </remarks>
    public async Task<TValue> GetAsync(Func<CancellationToken, Task<TValue>> load, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(load);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _timeProvider.GetUtcNow();
            var entry = _entry;
            var generation = Volatile.Read(ref _generation);
            if (entry is not null && now - entry.ProbedAt < PollInterval)
            {
                return entry.Value;
            }

            var stamp = await _source.GetStampAsync(_entityTypes, cancellationToken).ConfigureAwait(false);
            if (entry is not null && entry.Stamp == stamp && Volatile.Read(ref _generation) == generation)
            {
                _entry = entry with { ProbedAt = now };
                return entry.Value;
            }

            var value = await load(cancellationToken).ConfigureAwait(false);
            if (Volatile.Read(ref _generation) == generation)
            {
                _entry = new Entry(stamp, value, now);
            }

            return value;
        }
        finally
        {
            _gate.Release();
        }
    }



    /// <summary>
    /// Drops the cached value so the next <see cref="GetAsync"/> probes and reloads regardless of the interval,
    /// including a read that is already in flight: it may still answer with the copy it holds, but it will not
    /// put that copy back.
    /// </summary>
    public void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        _entry = null;
    }



    /// <summary>
    /// Releases the load gate. A disposed cache throws <see cref="ObjectDisposedException"/> from
    /// <see cref="GetAsync"/>.
    /// </summary>
    public void Dispose()
    {
        _gate.Dispose();
    }



    private sealed record Entry(RowVersionStamp Stamp, TValue Value, DateTimeOffset ProbedAt);
}
