// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Caching;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Infrastructure.Database.Settings;

namespace Wolfgang.Wms.UnitTests.Settings;

/// <summary>
/// E6.3 as a decorator: <see cref="CachedSettings"/> answers a repeated read from the current generation,
/// keeps scopes and keys apart, and lets every write through before dropping the generation.
/// </summary>
public sealed class CachedSettingsTests : IDisposable
{
    private static readonly SettingKey<int> MaxTotes = new("picking.max_totes", 3, "Totes a picker may carry.");
    private static readonly SettingKey<bool> Bulk = new("feature.bulk_picking", false, "Bulk picking.");
    private static readonly SettingRegistry Registry = new([Bulk, MaxTotes]);
    private static readonly SettingScopeRef Site1 = SettingScopeRef.Site(1);
    private static readonly SettingScopeRef Site2 = SettingScopeRef.Site(2);

    private readonly InMemorySettings _store = new(Registry);
    private readonly SettingsCache _cache = new(new ConstantRowVersionSource(), TimeProvider.System);
    private readonly CachedSettings _settings;



    public CachedSettingsTests()
    {
        _settings = new CachedSettings(_store, _cache);
    }



    [Fact]
    public async Task GetAsync_serves_a_repeated_read_from_the_generation()
    {
        var first = await _settings.GetAsync(MaxTotes, Site1, CancellationToken.None);
        var second = await _settings.GetAsync(MaxTotes, Site1, CancellationToken.None);
        var other = await _settings.GetAsync(MaxTotes, Site2, CancellationToken.None);

        Assert.Equal(3, first);
        Assert.Equal(3, second);
        Assert.Equal(3, other);
        Assert.Equal(2, _store.Reads);
        Assert.NotNull(_cache.CachedStamp);
    }



    [Fact]
    public async Task ListAsync_and_FindAsync_are_memoised_per_scope_and_key()
    {
        await _settings.ListAsync(Site1, CancellationToken.None);
        var list = await _settings.ListAsync(Site1, CancellationToken.None);
        await _settings.FindAsync(MaxTotes.Name, Site1, CancellationToken.None);
        var found = await _settings.FindAsync(MaxTotes.Name, Site1, CancellationToken.None);
        await _settings.FindAsync(Bulk.Name, Site1, CancellationToken.None);

        Assert.Equal(2, list.Count);
        Assert.Equal("3", found.EffectiveValue);
        Assert.Equal(3, _store.Reads);
        Assert.Equal(3, (await _cache.GetAsync(CancellationToken.None)).Count);
    }



    [Fact]
    public async Task An_unknown_key_is_refused_by_the_store_and_nothing_is_memoised()
    {
        var find = await Assert.ThrowsAsync<SettingException>(() => _settings.FindAsync("picking.nope", Site1, CancellationToken.None));
        var set = await Assert.ThrowsAsync<SettingException>(() => _settings.SetTextAsync("picking.nope", Site1, "1", "alice", CancellationToken.None));

        Assert.Equal(SettingErrorCodes.UnknownKey, find.Code);
        Assert.Equal(SettingErrorCodes.UnknownKey, set.Code);
        Assert.Equal(0, (await _cache.GetAsync(CancellationToken.None)).Count);
    }



    [Fact]
    public async Task Every_write_passes_through_and_drops_the_generation()
    {
        await _settings.GetAsync(MaxTotes, Site1, CancellationToken.None);
        var set = await _settings.SetAsync(MaxTotes, Site1, 4, "alice", CancellationToken.None);
        Assert.Null(_cache.CachedStamp);
        Assert.Equal(4, await _settings.GetAsync(MaxTotes, Site1, CancellationToken.None));

        await _settings.SetTextAsync(MaxTotes.Name, Site1, "5", "alice", CancellationToken.None);
        Assert.Equal(5, await _settings.GetAsync(MaxTotes, Site1, CancellationToken.None));

        await _settings.ResetAsync(MaxTotes, Site1, "alice", CancellationToken.None);
        Assert.Equal(3, await _settings.GetAsync(MaxTotes, Site1, CancellationToken.None));

        var mode = await _settings.SetModeAsync(MaxTotes, Site1, CascadeMode.PerZone, "alice", CancellationToken.None);
        Assert.Null(_cache.CachedStamp);

        Assert.Equal("4", set.ConfiguredValue);
        Assert.Equal(MaxTotes.Name, mode.Name);
        Assert.Equal(4, _store.Writes);
        Assert.Equal(4, _store.Reads);
    }



    [Fact]
    public void Constructor_refuses_a_null_dependency()
    {
        Assert.Throws<ArgumentNullException>(() => new CachedSettings(null!, _cache));
        Assert.Throws<ArgumentNullException>(() => new CachedSettings(_store, null!));
    }



    public void Dispose()
    {
        _cache.Dispose();
    }



    private sealed class ConstantRowVersionSource : IRowVersionSource
    {
        public Task<RowVersionStamp> GetStampAsync(IReadOnlyCollection<Type> entityTypes, CancellationToken cancellationToken)
        {
            return Task.FromResult(new RowVersionStamp(1, 1));
        }
    }
}
