// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Caching;

/// <summary>
/// A per-instance in-memory cache of one value invalidated by row version (E1.12, ADR 0003). The value is
/// reloaded only when the highest <c>row_version</c> of the tables it was built from has moved, and that
/// version is probed at most once per <see cref="PollInterval"/>, so a hot read costs nothing between polls
/// and at most one cheap version query otherwise. There is no shared cache component: every process holds its
/// own copy and the database is the source of truth.
/// </summary>
/// <typeparam name="TValue">The cached value; a read model built from the watched tables.</typeparam>
public sealed class VersionedCache<TValue> : IDisposable
    where TValue : class
{
    private readonly IRowVersionSource _source;
    private readonly IReadOnlyCollection<string> _tables;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Entry? _entry;



    /// <summary>
    /// Creates a cache over <paramref name="tables"/> that re-checks their row version at most once per
    /// <paramref name="pollInterval"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="tables"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pollInterval"/> is negative.</exception>
    public VersionedCache
    (
        IRowVersionSource source,
        IReadOnlyCollection<string> tables,
        TimeSpan pollInterval,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (pollInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), pollInterval, "The poll interval cannot be negative.");
        }

        if (tables.Count == 0)
        {
            throw new ArgumentException("A versioned cache must watch at least one table.", nameof(tables));
        }

        _source = source;
        _tables = tables;
        PollInterval = pollInterval;
        _timeProvider = timeProvider;
    }



    /// <summary>
    /// The minimum time between two row-version probes. Reads inside the interval are served from the copy.
    /// </summary>
    public TimeSpan PollInterval { get; }



    /// <summary>
    /// The row version the cached value was built at, or null when nothing is cached.
    /// </summary>
    public ulong? CachedVersion => _entry?.Version;



    /// <summary>
    /// The cached value, reloaded through <paramref name="load"/> when the watched tables' row version has
    /// moved since it was built. Concurrent callers share one load.
    /// </summary>
    /// <param name="load">Builds the value from the database; receives the cancellation token.</param>
    /// <param name="cancellationToken">Cancels the version probe or the load.</param>
    public async Task<TValue> GetAsync(Func<CancellationToken, Task<TValue>> load, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(load);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _timeProvider.GetUtcNow();
            var entry = _entry;
            if (entry is not null && now - entry.ProbedAt < PollInterval)
            {
                return entry.Value;
            }

            var version = await _source.GetMaxRowVersionAsync(_tables, cancellationToken).ConfigureAwait(false);
            if (entry is not null && entry.Version == version)
            {
                _entry = entry with { ProbedAt = now };
                return entry.Value;
            }

            var value = await load(cancellationToken).ConfigureAwait(false);
            _entry = new Entry(version, value, now);
            return value;
        }
        finally
        {
            _gate.Release();
        }
    }



    /// <summary>
    /// Drops the cached value so the next <see cref="GetAsync"/> probes and reloads regardless of the interval.
    /// </summary>
    public void Invalidate()
    {
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



    private sealed record Entry(ulong Version, TValue Value, DateTimeOffset ProbedAt);
}
