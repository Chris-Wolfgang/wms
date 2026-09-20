// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Mvc;

namespace Wolfgang.Wms.Core.Http.Paging;

/// <summary>
/// The paging parameters every list endpoint accepts (E82.3): a bidirectional keyset cursor (<c>after</c> or
/// <c>before</c>, never both), a page size capped at <see cref="MaxSize"/>, and an optional id range
/// (<c>id_from</c>/<c>id_to</c>) so parallel clients can split a table. Bind with <c>[AsParameters]</c>.
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
    /// Validates the combination and decodes the cursor; the error names the offending parameter so the
    /// endpoint can answer 400 with it.
    /// </summary>
    /// <param name="direction">Where to page from; <see cref="PageDirection.Forward"/> with no cursor means the first page.</param>
    /// <param name="cursor">The decoded cursor, or the default when none was sent.</param>
    /// <param name="error">Why the request is invalid, or null.</param>
    public bool TryResolve(out PageDirection direction, out Cursor cursor, out string? error)
    {
        direction = PageDirection.Forward;
        cursor = default;
        error = null;

        if (After is not null && Before is not null)
        {
            error = "Send either 'after' or 'before', not both.";
            return false;
        }

        if (IdFrom is not null && IdTo is not null && IdFrom > IdTo)
        {
            error = "'id_from' must not exceed 'id_to'.";
            return false;
        }

        var text = After ?? Before;
        if (text is null)
        {
            return true;
        }

        if (!Cursor.TryParse(text, out cursor))
        {
            error = After is not null ? "'after' is not a cursor this API issued." : "'before' is not a cursor this API issued.";
            return false;
        }

        direction = Before is not null ? PageDirection.Backward : PageDirection.Forward;
        return true;
    }
}
