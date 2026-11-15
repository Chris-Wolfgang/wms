// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Collections.Concurrent;
using Wolfgang.Wms.Core.Caching;
using Wolfgang.Wms.Infrastructure.Database.Caching;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// The per-instance cache of each user's "sessions valid after" (E10.5, ADR 0003): one
/// <see cref="VersionedCache{TValue}"/> over <c>core.user</c>, holding the answers given since the table's
/// highest <c>row_version</c> last moved. A revocation or a disablement on another instance is seen within
/// <see cref="PollInterval"/>; a save of a user row on this instance, whichever store made it, drops the cache
/// at once (<see cref="OwnWritesInvalidation"/>).
/// </summary>
public sealed class SessionRevocationsCache : IWatchingCache, IDisposable
{
    /// <summary>
    /// How often, at most, the table's version is probed on a read.
    /// </summary>
    public static TimeSpan PollInterval { get; } = TimeSpan.FromSeconds(5);



    private readonly VersionedCache<ConcurrentDictionary<long, DateTimeOffset?>> _cache;



    /// <summary>
    /// Creates the cache.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public SessionRevocationsCache(IRowVersionSource source, TimeProvider timeProvider)
    {
        _cache = new VersionedCache<ConcurrentDictionary<long, DateTimeOffset?>>(source, EntityTypes, PollInterval, timeProvider);
    }



    /// <inheritdoc/>
    public IReadOnlyCollection<Type> EntityTypes { get; } = [typeof(User)];



    /// <summary>
    /// The stamp the current answers were opened at, or null when nothing is cached.
    /// </summary>
    public RowVersionStamp? CachedStamp => _cache.CachedStamp;



    /// <summary>
    /// The current answers by user id, a new and empty set when the table has changed.
    /// </summary>
    public Task<ConcurrentDictionary<long, DateTimeOffset?>> GetAsync(CancellationToken cancellationToken)
    {
        return _cache.GetAsync(static _ => Task.FromResult(new ConcurrentDictionary<long, DateTimeOffset?>()), cancellationToken);
    }



    /// <summary>
    /// Drops the answers so the next read reloads.
    /// </summary>
    public void Invalidate()
    {
        _cache.Invalidate();
    }



    /// <inheritdoc/>
    public void Dispose()
    {
        _cache.Dispose();
    }
}
