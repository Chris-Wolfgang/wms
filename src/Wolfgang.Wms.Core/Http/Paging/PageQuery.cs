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
public sealed record PageQuery(PageDirection Direction, Cursor Cursor, SortOrder Sort, int Size)
{
    /// <summary>
    /// True when the request starts at the beginning of the sort (no cursor).
    /// </summary>
    public bool IsFirstPage => Cursor.Keys.Count == 0;



    /// <summary>
    /// The keyset comparison against the cursor's key values: true for <c>&gt;</c>, false for <c>&lt;</c>. Reading
    /// forward in an ascending sort, or backward in a descending one, seeks greater values; the other two
    /// combinations seek smaller ones. A backward read takes the rows nearest the cursor, then reverses them so the
    /// page is still in the requested sort.
    /// </summary>
    public bool SeeksGreater => (Direction == PageDirection.Forward) == (Sort.Direction == SortDirection.Ascending);
}
