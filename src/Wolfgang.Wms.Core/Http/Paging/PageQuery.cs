// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Http.Paging;

/// <summary>
/// A validated page request (E82.3), from <see cref="PageRequest.TryResolve"/>: which way to read, from which
/// position, in which sort, and how many rows.
/// </summary>
/// <param name="Direction">Forward (<c>after</c>, or the first page) or backward (<c>before</c>).</param>
/// <param name="Cursor">The position to read from; the default cursor (no keys) means the first page.</param>
/// <param name="Sort">The sort to serve; a cursor is only accepted in the sort it was issued under.</param>
/// <param name="Size">Rows per page, already clamped.</param>
/// <param name="IdFrom">Lower bound of the row id range (inclusive) the client asked for with <c>id_from</c>, so parallel
/// clients can split a table; null when unbounded.</param>
/// <param name="IdTo">Upper bound of the row id range (inclusive) the client asked for with <c>id_to</c>; null when
/// unbounded. Never below <paramref name="IdFrom"/>: <see cref="PageRequest.TryResolve"/> refuses that.</param>
public sealed record PageQuery(PageDirection Direction, Cursor Cursor, SortOrder Sort, int Size, long? IdFrom = null, long? IdTo = null)
{
    /// <summary>
    /// True when the request starts at the beginning of the sort (no cursor).
    /// </summary>
    public bool IsFirstPage => Cursor.IsEmpty;



    /// <summary>
    /// True when the client bounded the row ids with <c>id_from</c> or <c>id_to</c>; the endpoint must then apply
    /// the bounds to its query, or parallel clients splitting a table read overlapping pages.
    /// </summary>
    public bool HasIdRange => IdFrom is not null || IdTo is not null;



    /// <summary>
    /// The keyset comparison of the row's <c>(value, id)</c> against the cursor's <see cref="Cursor.Value"/> and
    /// <see cref="Cursor.Id"/> (the id alone for a sort on the id): true for <c>&gt;</c>, false for <c>&lt;</c>. Reading
    /// forward in an ascending sort, or backward in a descending one, seeks greater values; the other two
    /// combinations seek smaller ones. A backward read takes the rows nearest the cursor, then reverses them so the
    /// page is still in the requested sort.
    /// </summary>
    public bool SeeksGreater => (Direction == PageDirection.Forward) == (Sort.Direction == SortDirection.Ascending);
}
