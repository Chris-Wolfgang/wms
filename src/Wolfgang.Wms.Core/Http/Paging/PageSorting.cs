// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Http.Paging;

/// <summary>
/// The sorts one list endpoint serves (E82.3): the fields a client may sort on, each in either direction, and
/// the sort used when the request names none. Every field must be backed by an index that ends with the row id,
/// so sortable columns are opt-in per endpoint.
/// </summary>
public sealed class PageSorting
{
    private readonly HashSet<string> _fields;



    /// <summary>
    /// Declares the sortable fields; the default sort's field is always one of them.
    /// </summary>
    /// <param name="defaultSort">The sort when the request sends no <c>sort</c>.</param>
    /// <param name="fields">Further sortable fields, besides the default's.</param>
    /// <exception cref="ArgumentException">The default sort is unset, or a field is not a valid sort field.</exception>
    public PageSorting(SortOrder defaultSort, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (defaultSort.Field is null)
        {
            throw new ArgumentException("A default sort is required.", nameof(defaultSort));
        }

        _fields = new HashSet<string>(StringComparer.Ordinal) { defaultSort.Field };
        foreach (var field in fields)
        {
            _fields.Add(SortOrder.Ascending(field).Field);
        }

        Default = defaultSort;
        Fields = _fields.Order(StringComparer.Ordinal).ToList();
    }



    /// <summary>
    /// The sort used when the request names none.
    /// </summary>
    public SortOrder Default { get; }



    /// <summary>
    /// Every sortable field, ordered by name.
    /// </summary>
    public IReadOnlyList<string> Fields { get; }



    /// <summary>
    /// Resolves the <c>sort</c> query parameter: the default when it is absent, otherwise one of <see cref="Fields"/>
    /// in either direction.
    /// </summary>
    /// <param name="text">The <c>sort</c> parameter as sent, or null.</param>
    /// <param name="sort">The sort to serve.</param>
    /// <param name="error">Why the parameter is invalid (for a 400), or null.</param>
    public bool TryResolve(string? text, out SortOrder sort, out string? error)
    {
        error = null;
        if (text is null)
        {
            sort = Default;
            return true;
        }

        if (SortOrder.TryParse(text, out sort) && _fields.Contains(sort.Field))
        {
            return true;
        }

        sort = default;
        error = $"'sort' must be one of {string.Join(", ", Fields.SelectMany(f => new[] { f, "-" + f }))}.";
        return false;
    }
}
