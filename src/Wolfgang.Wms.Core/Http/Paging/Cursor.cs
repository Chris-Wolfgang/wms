// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Wolfgang.Wms.Core.Http.Paging;

/// <summary>
/// An opaque, stable keyset cursor (E82.3): the position of a row in one sort of an endpoint's list. Encoded as
/// URL-safe base64 so it can live in a shareable console URL; decoded only by the endpoint that issued it. The
/// payload is the sort it was issued under followed by the sort key value(s), joined by <c>|</c>, never a page
/// number. A cursor only means something in its own sort: <see cref="PageRequest.TryResolve"/> refuses it under
/// another, so a grid re-sorted by the user starts again from the first page instead of reading a wrong one.
/// </summary>
public readonly record struct Cursor
{
    private const char Separator = '|';
    private readonly IReadOnlyList<string>? _keys;



    private Cursor(SortOrder sort, IReadOnlyList<string> keys)
    {
        Sort = sort;
        _keys = keys;
    }



    /// <summary>
    /// The sort the cursor was issued under; the default value for the default cursor.
    /// </summary>
    public SortOrder Sort { get; }



    /// <summary>
    /// The sort key values the cursor points at, in <see cref="Sort"/> order (the sorted field, then the id as
    /// tiebreaker); empty for the default cursor.
    /// </summary>
    public IReadOnlyList<string> Keys => _keys ?? [];



    /// <summary>
    /// A cursor for a position in <paramref name="sort"/> given by its sort key values (the sorted field's value,
    /// then the id as tiebreaker).
    /// </summary>
    /// <exception cref="ArgumentException">The sort is unset, there are no keys, or a key is null or contains the separator.</exception>
    public static Cursor For(SortOrder sort, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (sort.Field is null)
        {
            throw new ArgumentException("A cursor needs the sort it was issued under.", nameof(sort));
        }

        if (keys.Length == 0)
        {
            throw new ArgumentException("A cursor needs at least one key.", nameof(keys));
        }

        if (keys.Any(key => key is null || key.Contains(Separator, StringComparison.Ordinal)))
        {
            throw new ArgumentException("A cursor key cannot be null or contain '|'.", nameof(keys));
        }

        return new Cursor(sort, keys);
    }



    /// <summary>
    /// A cursor for a position in <paramref name="sort"/> given by a numeric id (a sort on the id itself).
    /// </summary>
    /// <exception cref="ArgumentException">The sort is unset.</exception>
    public static Cursor For(SortOrder sort, long id)
    {
        return For(sort, id.ToString(CultureInfo.InvariantCulture));
    }



    /// <summary>
    /// Decodes a cursor received from a client; false when the text is not one this API issued.
    /// </summary>
    public static bool TryParse(string? text, out Cursor cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(text) || !Base64Url.IsValid(text))
        {
            return false;
        }

        var segments = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(text)).Split(Separator);
        if (segments.Length < 2 || !SortOrder.TryParse(segments[0], out var sort))
        {
            return false;
        }

        var keys = segments[1..];
        if (keys.Any(key => key.Length == 0 || key.Any(char.IsControl)))
        {
            return false;
        }

        cursor = new Cursor(sort, keys);
        return true;
    }



    /// <summary>
    /// The URL-safe encoded form for <c>next_cursor</c>, <c>after</c> and <c>before</c>; empty for the default
    /// cursor.
    /// </summary>
    public string Encode()
    {
        return _keys is null
            ? string.Empty
            : Base64Url.EncodeToString(Encoding.UTF8.GetBytes(Sort.ToString() + Separator + string.Join(Separator, _keys)));
    }



    /// <inheritdoc/>
    public override string ToString()
    {
        return Encode();
    }



    /// <inheritdoc/>
    public bool Equals(Cursor other)
    {
        return Sort.Equals(other.Sort) && Keys.SequenceEqual(other.Keys, StringComparer.Ordinal);
    }



    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Sort);
        foreach (var key in Keys)
        {
            hash.Add(key, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
