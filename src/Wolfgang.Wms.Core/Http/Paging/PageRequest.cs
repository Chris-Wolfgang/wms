// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;

namespace Wolfgang.Wms.Core.Http.Paging;

/// <summary>
/// The paging parameters every list endpoint accepts (E82.3): a bidirectional keyset cursor (<c>after</c> or
/// <c>before</c>, never both), a sort (<c>sort</c>, one the endpoint declares in its <see cref="PageSorting"/>), a
/// page size capped at <see cref="MaxSize"/>, and an optional id range (<c>id_from</c>/<c>id_to</c>) so parallel
/// clients can split a table. Bind with <c>[AsParameters]</c>.
/// </summary>
public sealed record PageRequest
{
    /// <summary>
    /// Page size when the client sends none.
    /// </summary>
    public const int DefaultSize = 50;



    /// <summary>
    /// Largest page any endpoint serves; a larger request is clamped, not refused.
    /// </summary>
    public const int MaxSize = 500;



    /// <summary>
    /// Cursor of the last item of the previous page (forward paging).
    /// </summary>
    [FromQuery(Name = "after")]
    public string? After { get; init; }



    /// <summary>
    /// Cursor of the first item of the next page (backward paging).
    /// </summary>
    [FromQuery(Name = "before")]
    public string? Before { get; init; }



    /// <summary>
    /// Requested sort: a field name for ascending, <c>-</c> plus the field for descending; the endpoint's
    /// default when absent.
    /// </summary>
    [FromQuery(Name = "sort")]
    public string? Sort { get; init; }



    /// <summary>
    /// Requested page size; clamped to 1..<see cref="MaxSize"/> by <see cref="EffectiveSize"/>.
    /// </summary>
    [FromQuery(Name = "size")]
    public int? Size { get; init; }



    /// <summary>
    /// Lower bound of the id range (inclusive), for parallel clients splitting a table.
    /// </summary>
    [FromQuery(Name = "id_from")]
    public long? IdFrom { get; init; }



    /// <summary>
    /// Upper bound of the id range (inclusive), for parallel clients splitting a table.
    /// </summary>
    [FromQuery(Name = "id_to")]
    public long? IdTo { get; init; }



    /// <summary>
    /// The page size the endpoint uses: the default when none was sent, otherwise clamped to 1..<see cref="MaxSize"/>.
    /// </summary>
    public int EffectiveSize => Size is null ? DefaultSize : Math.Clamp(Size.Value, 1, MaxSize);



    /// <summary>
    /// Validates the combination against the endpoint's sorts and decodes the cursor; the error names the
    /// offending parameter so the endpoint can answer 400 with it. A cursor issued under another sort is refused,
    /// never read as a position in this one: a client that changes the sort starts from the first page.
    /// </summary>
    /// <param name="sorting">The sorts the endpoint serves.</param>
    /// <param name="query">The validated request, or null when invalid.</param>
    /// <param name="error">Why the request is invalid, or null.</param>
    public bool TryResolve(PageSorting sorting, [NotNullWhen(true)] out PageQuery? query, out string? error)
    {
        ArgumentNullException.ThrowIfNull(sorting);
        query = null;

        error = ValidateCombination();
        if (error is not null || !sorting.TryResolve(Sort, out var sort, out error))
        {
            return false;
        }

        var parameter = After is not null ? "after" : "before";
        var text = After ?? Before;
        var cursor = default(Cursor);
        if (text is not null && !Cursor.TryParse(text, out cursor))
        {
            error = $"'{parameter}' is not a cursor this API issued.";
            return false;
        }

        if (text is not null && !cursor.Sort.Equals(sort))
        {
            error = $"'{parameter}' was issued for sort '{cursor.Sort}', not '{sort}'; after changing the sort, start again from the first page.";
            return false;
        }

        query = new PageQuery(Before is not null ? PageDirection.Backward : PageDirection.Forward, cursor, sort, EffectiveSize);
        return true;
    }



    private string? ValidateCombination()
    {
        if (After is not null && Before is not null)
        {
            return "Send either 'after' or 'before', not both.";
        }

        return IdFrom is not null && IdTo is not null && IdFrom > IdTo ? "'id_from' must not exceed 'id_to'." : null;
    }
}
