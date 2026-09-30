// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Http.Paging;

/// <summary>
/// A sort a list endpoint can serve (E82.3): one field and a direction, written in the <c>sort</c> query parameter
/// as the field name for ascending (<c>created_at</c>) or with a leading <c>-</c> for descending
/// (<c>-created_at</c>). The endpoint adds the row id as the tiebreaker, so the order is total and keyset paging
/// is stable. Field names are lower-case snake_case, as in the JSON the API returns.
/// </summary>
public readonly record struct SortOrder
{
    /// <summary>
    /// Longest accepted field name.
    /// </summary>
    public const int MaxFieldLength = 64;



    /// <summary>
    /// Creates a sort on <paramref name="field"/> in <paramref name="direction"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="field"/> is not a lower-case snake_case name.</exception>
    public SortOrder(string field, SortDirection direction)
    {
        if (!IsFieldName(field))
        {
            throw new ArgumentException("A sort field is a lower-case snake_case name (a-z, 0-9, _) starting with a letter.", nameof(field));
        }

        Field = field;
        Direction = direction;
    }



    /// <summary>
    /// The field sorted on; null for the default value.
    /// </summary>
    public string Field { get; }



    /// <summary>
    /// The order the field is sorted in.
    /// </summary>
    public SortDirection Direction { get; }



    /// <summary>
    /// An ascending sort on <paramref name="field"/>.
    /// </summary>
    public static SortOrder Ascending(string field)
    {
        return new SortOrder(field, SortDirection.Ascending);
    }



    /// <summary>
    /// A descending sort on <paramref name="field"/>.
    /// </summary>
    public static SortOrder Descending(string field)
    {
        return new SortOrder(field, SortDirection.Descending);
    }



    /// <summary>
    /// Parses the <c>sort</c> query parameter form (<c>field</c> or <c>-field</c>); false for anything else.
    /// </summary>
    public static bool TryParse(string? text, out SortOrder sort)
    {
        sort = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var descending = text[0] == '-';
        var field = descending ? text[1..] : text;
        if (!IsFieldName(field))
        {
            return false;
        }

        sort = new SortOrder(field, descending ? SortDirection.Descending : SortDirection.Ascending);
        return true;
    }



    /// <summary>
    /// The <c>sort</c> query parameter form; empty for the default value.
    /// </summary>
    public override string ToString()
    {
        if (Field is null)
        {
            return string.Empty;
        }

        return Direction == SortDirection.Descending ? "-" + Field : Field;
    }



    private static bool IsFieldName(string? field)
    {
        return field is { Length: > 0 and <= MaxFieldLength }
            && field[0] is >= 'a' and <= 'z'
            && field.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');
    }
}
