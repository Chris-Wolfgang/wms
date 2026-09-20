// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Wolfgang.Wms.Core.Http.Paging;

/// <summary>
/// An opaque, stable keyset cursor (E82.3): the position of a row in an endpoint's fixed indexed sort. Encoded
/// as URL-safe base64 so it can live in a shareable console URL; decoded only by the endpoint that issued it.
/// The payload is the sort key value(s) joined by <c>|</c>, never a page number.
/// </summary>
public readonly record struct Cursor
{
    private const char Separator = '|';
    private readonly IReadOnlyList<string>? _keys;



    private Cursor(IReadOnlyList<string> keys)
    {
        _keys = keys;
    }



    /// <summary>
    /// The sort key values the cursor points at, in the endpoint's sort order; empty for the default cursor.
    /// </summary>
    public IReadOnlyList<string> Keys => _keys ?? [];



    /// <summary>
    /// A cursor for a position given by its sort key values (an id, or a timestamp plus an id as tiebreaker).
    /// </summary>
    /// <exception cref="ArgumentException">No keys, or a key is null or contains the separator.</exception>
    public static Cursor For(params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Length == 0)
        {
            throw new ArgumentException("A cursor needs at least one key.", nameof(keys));
        }

        foreach (var key in keys)
        {
            if (key is null || key.Contains(Separator, StringComparison.Ordinal))
            {
                throw new ArgumentException("A cursor key cannot be null or contain '|'.", nameof(keys));
            }
        }

        return new Cursor(keys);
    }



    /// <summary>
    /// A cursor for a position given by a numeric id.
    /// </summary>
    public static Cursor For(long id)
    {
        return For(id.ToString(CultureInfo.InvariantCulture));
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

        var payload = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(text));
        var keys = payload.Split(Separator);
        if (payload.Length == 0 || keys.Any(key => key.Length == 0 || key.Any(char.IsControl)))
        {
            return false;
        }

        cursor = new Cursor(keys);
        return true;
    }



    /// <summary>
    /// The URL-safe encoded form for <c>next_cursor</c>, <c>after</c> and <c>before</c>.
    /// </summary>
    public string Encode()
    {
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(string.Join(Separator, Keys)));
    }



    /// <inheritdoc/>
    public override string ToString()
    {
        return Encode();
    }



    /// <inheritdoc/>
    public bool Equals(Cursor other)
    {
        return Keys.SequenceEqual(other.Keys, StringComparer.Ordinal);
    }



    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var key in Keys)
        {
            hash.Add(key, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
