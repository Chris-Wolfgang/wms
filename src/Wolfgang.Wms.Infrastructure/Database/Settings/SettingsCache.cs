// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Caching;

namespace Wolfgang.Wms.Infrastructure.Database.Settings;

/// <summary>
/// The per-instance settings cache (E6.3, ADR 0003): one <see cref="VersionedCache{TValue}"/> over
/// <c>core.setting</c>, reloaded only when the table's highest <c>row_version</c> moves, probed at most once
/// per <see cref="PollInterval"/>, and invalidated outright by this instance's own writes.
/// </summary>
public sealed class SettingsCache : IDisposable
{
    /// <summary>
    /// The table the cache watches.
    /// </summary>
    public const string Table = SettingConfiguration.Schema + "." + SettingConfiguration.Table;



    /// <summary>
    /// How often, at most, the table's version is probed on a read.
    /// </summary>
    public static TimeSpan PollInterval { get; } = TimeSpan.FromSeconds(5);



    private readonly VersionedCache<SettingsSnapshot> _cache;



    /// <summary>
    /// Creates the cache.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public SettingsCache(IRowVersionSource source, TimeProvider timeProvider)
    {
        _cache = new VersionedCache<SettingsSnapshot>(source, [Table], PollInterval, timeProvider);
    }



    /// <summary>
    /// The version the cached snapshot was built at, or null when nothing is cached.
    /// </summary>
    public ulong? CachedVersion => _cache.CachedVersion;



    /// <summary>
    /// The current snapshot, loading it through <paramref name="load"/> when the table has changed.
    /// </summary>
    public Task<SettingsSnapshot> GetAsync(Func<CancellationToken, Task<SettingsSnapshot>> load, CancellationToken cancellationToken)
    {
        return _cache.GetAsync(load, cancellationToken);
    }



    /// <summary>
    /// Drops the snapshot so the next read reloads (called after this instance writes).
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
