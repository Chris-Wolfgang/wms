// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Http.Paging;

/// <summary>
/// One keyset page of a list endpoint (E82.3): the items, opaque cursors for the neighbouring pages, the exact
/// total in scope, and the id bounds so a parallel client can split the range with <c>id_from</c>/<c>id_to</c>.
/// </summary>
/// <typeparam name="TItem">The API record the endpoint projects to.</typeparam>
/// <param name="Items">The page, in the endpoint's fixed sort.</param>
/// <param name="NextCursor">Cursor for the page after this one (<c>after=</c>), or null on the last page.</param>
/// <param name="PreviousCursor">Cursor for the page before this one (<c>before=</c>), or null on the first page.</param>
/// <param name="TotalCount">Exact number of rows matching the filters in scope, not just this page.</param>
/// <param name="MinId">Lowest id matching the filters in scope, or null when empty.</param>
/// <param name="MaxId">Highest id matching the filters in scope, or null when empty.</param>
public sealed record Page<TItem>
(
    IReadOnlyList<TItem> Items,
    string? NextCursor,
    string? PreviousCursor,
    long TotalCount,
    long? MinId,
    long? MaxId
)
{
    /// <summary>
    /// The items on this page, never null.
    /// </summary>
    public IReadOnlyList<TItem> Items { get; } = Items ?? throw new ArgumentNullException(nameof(Items));



    /// <summary>
    /// Exact total in scope; never negative.
    /// </summary>
    public long TotalCount { get; } = TotalCount >= 0
        ? TotalCount
        : throw new ArgumentOutOfRangeException(nameof(TotalCount), TotalCount, "The total count cannot be negative.");
}



/// <summary>
/// Factories for <see cref="Page{TItem}"/>.
/// </summary>
public static class Page
{
    /// <summary>
    /// An empty page: no items, no cursors, zero total, no id bounds.
    /// </summary>
    public static Page<TItem> Empty<TItem>()
    {
        return new Page<TItem>(Items: [], NextCursor: null, PreviousCursor: null, TotalCount: 0, MinId: null, MaxId: null);
    }
}
