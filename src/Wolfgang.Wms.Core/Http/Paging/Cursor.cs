// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Wolfgang.Wms.Core.Http.Paging;

/// <summary>
/// An opaque, stable keyset cursor (E82.3): the position of a row in one sort of an endpoint's list. Encoded as
/// URL-safe base64 so it can live in a shareable console URL; decoded only by the endpoint that issued it. A
/// sort is one field in one direction, and the row id breaks ties in that same direction, so a position is the
/// sorted field's <see cref="Value"/> and the row's <see cref="Id"/> (only the id when the sort is on
/// <see cref="IdField"/> itself). The payload is <c>sort|value|id</c> or <c>sort|id</c>, never a page number. A
/// cursor only means something in its own sort: <see cref="PageRequest.TryResolve"/> refuses it under another,
/// so a grid re-sorted by the user starts again from the first page instead of reading a wrong one.
/// </summary>
public readonly record struct Cursor
{
    /// <summary>
    /// The row id field: the tiebreaker of every sort, and a sort of its own.
    /// </summary>
    public const string IdField = "id";

    private const char Separator = '|';



    private Cursor(SortOrder sort, string? value, long id)
    {
        Sort = sort;
        Value = value;
        Id = id;
    }



    /// <summary>
    /// The sort the cursor was issued under; the default value for the default cursor.
    /// </summary>
    public SortOrder Sort { get; }



    /// <summary>
    /// The sorted field's value at the position, in the field's invariant text form; null when the sort is on
    /// <see cref="IdField"/> and for the default cursor.
    /// </summary>
    public string? Value { get; }



    /// <summary>
    /// The row id at the position: the tiebreaker, ordered in the same direction as <see cref="Sort"/>.
    /// </summary>
    public long Id { get; }



    /// <summary>
    /// True for the default cursor, which points at no row (the first page).
    /// </summary>
    public bool IsEmpty => Sort.Field is null;



    /// <summary>
    /// A cursor for a position in a sort on a field other than <see cref="IdField"/>: that field's value and the
    /// row id as tiebreaker.
    /// </summary>
    /// <exception cref="ArgumentException">The sort is unset or is on <see cref="IdField"/>; the value is empty or
    /// contains the separator or a control character; the id is negative.</exception>
    public static Cursor For(SortOrder sort, string value, long id)
    {
        RequireSort(sort);
        if (IsIdSort(sort))
        {
            throw new ArgumentException($"A sort on '{IdField}' has no separate value; use For(sort, id).", nameof(sort));
        }

        if (!IsValue(value))
        {
            throw new ArgumentException("A cursor value cannot be empty or contain '|' or control characters.", nameof(value));
        }

        RequireId(id);
        return new Cursor(sort, value, id);
    }



    /// <summary>
    /// A cursor for a position in a sort on <see cref="IdField"/> itself.
    /// </summary>
    /// <exception cref="ArgumentException">The sort is unset or is on another field; the id is negative.</exception>
    public static Cursor For(SortOrder sort, long id)
    {
        RequireSort(sort);
        if (!IsIdSort(sort))
        {
            throw new ArgumentException($"A sort on '{sort.Field}' needs the field's value; use For(sort, value, id).", nameof(sort));
        }

        RequireId(id);
        return new Cursor(sort, value: null, id);
    }



    /// <summary>
    /// Decodes a cursor received from a client; false when the text is not one this API issued: not base64url, an
    /// invalid sort, the wrong number of segments for the sort, an empty or control-character value, or an id that
    /// is not a non-negative integer.
    /// </summary>
    public static bool TryParse(string? text, out Cursor cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(text) || !Base64Url.IsValid(text))
        {
            return false;
        }

        var segments = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(text)).Split(Separator);
        if (!SortOrder.TryParse(segments[0], out var sort) || segments.Length != (IsIdSort(sort) ? 2 : 3))
        {
            return false;
        }

        var value = segments.Length == 3 ? segments[1] : null;
        if ((value is not null && !IsValue(value))
            || !long.TryParse(segments[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            return false;
        }

        cursor = new Cursor(sort, value, id);
        return true;
    }



    /// <summary>
    /// The URL-safe encoded form for <c>next_cursor</c>, <c>after</c> and <c>before</c>; empty for the default
    /// cursor.
    /// </summary>
    public string Encode()
    {
        if (IsEmpty)
        {
            return string.Empty;
        }

        var id = Id.ToString(CultureInfo.InvariantCulture);
        var payload = Value is null
            ? $"{Sort}{Separator}{id}"
            : $"{Sort}{Separator}{Value}{Separator}{id}";
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload));
    }



    /// <inheritdoc/>
    public override string ToString()
    {
        return Encode();
    }



    private static bool IsIdSort(SortOrder sort)
    {
        return string.Equals(sort.Field, IdField, StringComparison.Ordinal);
    }



    private static bool IsValue(string? value)
    {
        return !string.IsNullOrEmpty(value)
            && !value.Contains(Separator, StringComparison.Ordinal)
            && !value.Any(char.IsControl);
    }



    private static void RequireSort(SortOrder sort)
    {
        if (sort.Field is null)
        {
            throw new ArgumentException("A cursor needs the sort it was issued under.", nameof(sort));
        }
    }



    private static void RequireId(long id)
    {
        if (id < 0)
        {
            throw new ArgumentException("A row id cannot be negative.", nameof(id));
        }
    }
}
