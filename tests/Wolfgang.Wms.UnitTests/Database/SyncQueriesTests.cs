// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Infrastructure.Database.Sync;
using Wolfgang.Wms.UnitTests.Database.TestModels;

namespace Wolfgang.Wms.UnitTests.Database;

/// <summary>
/// E5.3 watermark arithmetic without a database; the queries themselves run against real engines in the
/// integration tests.
/// </summary>
public sealed class SyncQueriesTests
{
    [Fact]
    public void NextSince_while_more_pages_exist_is_the_last_version_read()
    {
        Assert.Equal(700, SyncQueries.NextSince(since: 100, lastVersionRead: 700, hasMore: true));
    }



    [Fact]
    public void NextSince_on_the_final_page_steps_back_one_safety_margin()
    {
        Assert.Equal(100, SyncQueries.SafetyMargin);
        Assert.Equal(600, SyncQueries.NextSince(since: 100, lastVersionRead: 700, hasMore: false));
    }



    [Theory]
    [InlineData(650, 700, false)]
    [InlineData(100, 100, false)]
    [InlineData(100, 50, true)]
    public void NextSince_never_falls_below_what_the_client_sent(long since, long last, bool hasMore)
    {
        Assert.Equal(since, SyncQueries.NextSince(since, last, hasMore));
    }



    [Fact]
    public void Page_sizes_are_bounded()
    {
        Assert.Equal(500, SyncQueries.DefaultPageSize);
        Assert.Equal(5000, SyncQueries.MaxPageSize);
    }



    [Fact]
    public void A_delta_never_carries_null_items()
    {
        Assert.Throws<ArgumentNullException>(() => new Delta<int>(null!, 0, false));
        Assert.Equal([1, 2], new Delta<int>([1, 2], 5, true).Items);
    }



    [Fact]
    public async Task Queries_when_an_argument_is_null_throw()
    {
        var source = Array.Empty<Sku>().AsQueryable();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await SyncQueries.DeltaAsync<Sku, long>(null!, 0, 1, s => s.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await SyncQueries.DeltaAsync<Sku, long>(source, 0, 1, null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await SyncQueries.ManifestAsync<Sku>(null!, CancellationToken.None));
    }
}
