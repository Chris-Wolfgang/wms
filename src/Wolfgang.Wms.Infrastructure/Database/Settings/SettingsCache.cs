// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Caching;
using Wolfgang.Wms.Infrastructure.Database.Caching;
using Wolfgang.Wms.Infrastructure.Zones;

namespace Wolfgang.Wms.Infrastructure.Database.Settings;

/// <summary>
/// The per-instance settings cache (E6.3, ADR 0003): one <see cref="VersionedCache{TValue}"/> whose value is
/// the current <see cref="SettingsMemo"/> generation. A fresh generation is published when the highest
/// <c>row_version</c> of <c>core.setting</c> or of <c>core.zone</c> moves (a zone's site decides what a
/// zone-scoped read resolves to), probed at most once per <see cref="PollInterval"/>, and outright when this
/// instance saves either (<see cref="OwnWritesInvalidation"/>) or writes through <see cref="CachedSettings"/>.
/// </summary>
public sealed class SettingsCache : IWatchingCache, IDisposable
{
    /// <summary>
    /// The table the cache watches first.
    /// </summary>
    public const string Table = SettingConfiguration.Schema + "." + SettingConfiguration.Table;



    /// <summary>
    /// How often, at most, the tables' versions are probed on a read.
    /// </summary>
    public static TimeSpan PollInterval { get; } = TimeSpan.FromSeconds(5);



    private readonly VersionedCache<SettingsMemo> _cache;



    /// <summary>
    /// Creates the cache.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public SettingsCache(IRowVersionSource source, TimeProvider timeProvider)
    {
        _cache = new VersionedCache<SettingsMemo>(source, EntityTypes, PollInterval, timeProvider);
    }



    /// <inheritdoc/>
    public IReadOnlyCollection<Type> EntityTypes { get; } = [typeof(Setting), typeof(Zone)];



    /// <summary>
    /// The stamp (highest row version and row count) the current generation was opened at, or null when
    /// nothing is cached.
    /// </summary>
    public RowVersionStamp? CachedStamp => _cache.CachedStamp;



    /// <summary>
    /// The current generation, a new and empty one when the tables have changed.
    /// </summary>
    public Task<SettingsMemo> GetAsync(CancellationToken cancellationToken)
    {
        return _cache.GetAsync(static _ => Task.FromResult(new SettingsMemo()), cancellationToken);
    }



    /// <summary>
    /// Drops the generation so the next read opens a new one.
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
