// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Database.Sync;

/// <summary>
/// One page of changes since a watermark (E5.3): rows (including soft-deleted ones) ordered by
/// <c>row_version</c>, and the watermark the client stores for its next call. Clients apply rows as
/// idempotent upserts, so re-reading a few rows on the next call is harmless.
/// </summary>
/// <typeparam name="TItem">The API record the rows project to.</typeparam>
/// <param name="Items">Changed rows in <c>row_version</c> order.</param>
/// <param name="NextSince">The watermark for the next call; never lower than the one the client sent.</param>
/// <param name="HasMore">True when more changed rows exist beyond this page; call again with <see cref="NextSince"/> now.</param>
public sealed record Delta<TItem>(IReadOnlyList<TItem> Items, long NextSince, bool HasMore)
{
    /// <summary>
    /// The rows, never null.
    /// </summary>
    public IReadOnlyList<TItem> Items { get; } = Items ?? throw new ArgumentNullException(nameof(Items));
}
