// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Caching;

namespace Wolfgang.Wms.UnitTests.Caching;

public sealed class VersionedCacheTests
{
    private static readonly string[] Tables = ["release", "release_line"];
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);
    private readonly FakeRowVersionSource _source = new();
    private readonly FakeTimeProvider _clock = new();
    private int _loads;



    [Fact]
    public async Task GetAsync_on_first_call_probes_and_loads()
    {
        using var cache = CreateCache();
        _source.Version = 5;

        var value = await cache.GetAsync(Load, CancellationToken.None);

        Assert.Equal("value 1", value);
        Assert.Equal(1, _source.Probes);
        Assert.Equal(1, _loads);
        Assert.Equal(5UL, cache.CachedVersion);
    }



    [Fact]
    public async Task GetAsync_inside_the_poll_interval_serves_the_copy_without_probing()
    {
        using var cache = CreateCache();
        await cache.GetAsync(Load, CancellationToken.None);
        _source.Version = 99;
        _clock.Advance(Interval - TimeSpan.FromMilliseconds(1));

        var value = await cache.GetAsync(Load, CancellationToken.None);

        Assert.Equal("value 1", value);
        Assert.Equal(1, _source.Probes);
        Assert.Equal(1, _loads);
    }



    [Fact]
    public async Task GetAsync_after_the_interval_with_an_unchanged_version_probes_but_does_not_reload()
    {
        using var cache = CreateCache();
        await cache.GetAsync(Load, CancellationToken.None);
        _clock.Advance(Interval);

        var value = await cache.GetAsync(Load, CancellationToken.None);
        _clock.Advance(Interval - TimeSpan.FromMilliseconds(1));
        await cache.GetAsync(Load, CancellationToken.None);

        Assert.Equal("value 1", value);
        Assert.Equal(2, _source.Probes);
        Assert.Equal(1, _loads);
    }



    [Fact]
    public async Task GetAsync_after_the_interval_with_a_moved_version_reloads()
    {
        using var cache = CreateCache();
        await cache.GetAsync(Load, CancellationToken.None);
        _source.Version = 6;
        _clock.Advance(Interval);

        var value = await cache.GetAsync(Load, CancellationToken.None);

        Assert.Equal("value 2", value);
        Assert.Equal(2, _loads);
        Assert.Equal(6UL, cache.CachedVersion);
    }



    [Fact]
    public async Task Invalidate_forces_a_probe_and_reload_inside_the_interval()
    {
        using var cache = CreateCache();
        await cache.GetAsync(Load, CancellationToken.None);

        cache.Invalidate();
        var versionAfterInvalidate = cache.CachedVersion;
        var value = await cache.GetAsync(Load, CancellationToken.None);

        Assert.Null(versionAfterInvalidate);
        Assert.Equal("value 2", value);
        Assert.Equal(2, _source.Probes);
        Assert.Equal(2, _loads);
    }



    [Fact]
    public async Task GetAsync_when_called_concurrently_shares_one_load()
    {
        using var cache = CreateCache();
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = cache.GetAsync(_ => release.Task, CancellationToken.None);
        var second = cache.GetAsync(Load, CancellationToken.None);
        release.SetResult("shared");
        var values = await Task.WhenAll(first, second);

        Assert.Equal(["shared", "shared"], values);
        Assert.Equal(0, _loads);
        Assert.Equal(1, _source.Probes);
    }



    [Fact]
    public async Task GetAsync_when_cancelled_during_the_probe_propagates_and_caches_nothing()
    {
        using var cache = CreateCache();
        using var cancellation = new CancellationTokenSource();
        _source.OnProbe = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetAsync(Load, cancellation.Token));

        Assert.Null(cache.CachedVersion);
        Assert.Equal(0, _loads);
    }



    [Fact]
    public async Task GetAsync_when_load_is_null_throws()
    {
        using var cache = CreateCache();

        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.GetAsync(null!, CancellationToken.None));
    }



    [Fact]
    public void Constructor_exposes_the_poll_interval()
    {
        using var cache = CreateCache();

        Assert.Equal(Interval, cache.PollInterval);
    }



    [Fact]
    public async Task GetAsync_after_Dispose_throws()
    {
        var cache = CreateCache();
        cache.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.GetAsync(Load, CancellationToken.None));
    }



    [Fact]
    public void Constructor_when_an_argument_is_invalid_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new VersionedCache<string>(null!, Tables, Interval, _clock));
        Assert.Throws<ArgumentNullException>(() => new VersionedCache<string>(_source, null!, Interval, _clock));
        Assert.Throws<ArgumentNullException>(() => new VersionedCache<string>(_source, Tables, Interval, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VersionedCache<string>(_source, Tables, TimeSpan.FromSeconds(-1), _clock));
        Assert.Throws<ArgumentException>(() => new VersionedCache<string>(_source, [], Interval, _clock));
    }



    private VersionedCache<string> CreateCache()
    {
        return new VersionedCache<string>(_source, Tables, Interval, _clock);
    }



    private Task<string> Load(CancellationToken cancellationToken)
    {
        _loads++;
        return Task.FromResult($"value {_loads}");
    }



    private sealed class FakeRowVersionSource : IRowVersionSource
    {
        public ulong Version { get; set; }

        public int Probes { get; private set; }

        public Action? OnProbe { get; set; }



        public Task<ulong> GetMaxRowVersionAsync(IReadOnlyCollection<string> tables, CancellationToken cancellationToken)
        {
            Assert.Equal(Tables, tables);
            Probes++;
            OnProbe?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Version);
        }
    }



    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);



        public void Advance(TimeSpan by)
        {
            _now += by;
        }



        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }
}
