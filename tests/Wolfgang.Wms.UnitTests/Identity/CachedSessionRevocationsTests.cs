// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Caching;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Infrastructure.Identity;

namespace Wolfgang.Wms.UnitTests.Identity;

/// <summary>
/// E10.5 as a decorator: <see cref="CachedSessionRevocations"/> answers a repeated "sessions valid after"
/// from the cache (a null answer included), and a revocation passes through and drops the cache.
/// </summary>
public sealed class CachedSessionRevocationsTests : IDisposable
{
    private static readonly DateTimeOffset RevokedAt = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    private readonly RecordingRevocations _store = new();
    private readonly SessionRevocationsCache _cache = new(new ConstantRowVersionSource(), TimeProvider.System);
    private readonly CachedSessionRevocations _revocations;



    public CachedSessionRevocationsTests()
    {
        _revocations = new CachedSessionRevocations(_store, _cache);
    }



    [Fact]
    public async Task SessionsValidAfterAsync_serves_a_repeated_answer_from_the_cache()
    {
        _store.Answers[7] = RevokedAt;

        var first = await _revocations.SessionsValidAfterAsync(7, CancellationToken.None);
        var second = await _revocations.SessionsValidAfterAsync(7, CancellationToken.None);
        var none = await _revocations.SessionsValidAfterAsync(8, CancellationToken.None);
        var noneAgain = await _revocations.SessionsValidAfterAsync(8, CancellationToken.None);

        Assert.Equal(RevokedAt, first);
        Assert.Equal(RevokedAt, second);
        Assert.Null(none);
        Assert.Null(noneAgain);
        Assert.Equal(2, _store.Reads);
        Assert.NotNull(_cache.CachedStamp);
    }



    [Fact]
    public async Task RevokeSessionsAsync_passes_through_and_drops_the_cache()
    {
        await _revocations.SessionsValidAfterAsync(7, CancellationToken.None);
        await _revocations.RevokeSessionsAsync(7, CancellationToken.None);
        var after = await _revocations.SessionsValidAfterAsync(7, CancellationToken.None);

        Assert.Equal([7L], _store.Revoked);
        Assert.Equal(RevokedAt, after);
        Assert.Equal(2, _store.Reads);
    }



    [Fact]
    public void Constructor_refuses_a_null_dependency()
    {
        Assert.Throws<ArgumentNullException>(() => new CachedSessionRevocations(null!, _cache));
        Assert.Throws<ArgumentNullException>(() => new CachedSessionRevocations(_store, null!));
    }



    public void Dispose()
    {
        _cache.Dispose();
    }



    private sealed class RecordingRevocations : ISessionRevocations
    {
        public Dictionary<long, DateTimeOffset?> Answers { get; } = [];

        public List<long> Revoked { get; } = [];

        public int Reads { get; private set; }



        public Task<DateTimeOffset?> SessionsValidAfterAsync(long userId, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(Answers.GetValueOrDefault(userId));
        }



        public Task RevokeSessionsAsync(long userId, CancellationToken cancellationToken)
        {
            Revoked.Add(userId);
            Answers[userId] = RevokedAt;
            return Task.CompletedTask;
        }
    }



    private sealed class ConstantRowVersionSource : IRowVersionSource
    {
        public Task<RowVersionStamp> GetStampAsync(IReadOnlyCollection<Type> entityTypes, CancellationToken cancellationToken)
        {
            return Task.FromResult(new RowVersionStamp(1, 1));
        }
    }
}
