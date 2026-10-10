// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Http.Paging;

namespace Wolfgang.Wms.UnitTests.Http;

public sealed class PagingTests
{
    private static readonly SortOrder ById = SortOrder.Ascending("id");

    private static readonly SortOrder NewestFirst = SortOrder.Descending("created_at");

    private static readonly PageSorting Sorting = new(NewestFirst, "id", "sku");



    [Fact]
    public void Cursor_round_trips_its_sort_value_and_id_through_an_opaque_url_safe_string()
    {
        var cursor = Cursor.For(NewestFirst, "2026-09-19T12:00:00Z", 42);

        var encoded = cursor.Encode();
        var parsed = Cursor.TryParse(encoded, out var back);

        Assert.True(parsed);
        Assert.Equal(cursor, back);
        Assert.Equal(NewestFirst, back.Sort);
        Assert.Equal("2026-09-19T12:00:00Z", back.Value);
        Assert.Equal(42, back.Id);
        Assert.False(back.IsEmpty);
        Assert.Equal(encoded, cursor.ToString());
        Assert.Equal("2|-created_at|42|2026-09-19T12:00:00Z", Decode(encoded));
        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
        Assert.DoesNotContain('=', encoded);
    }



    [Fact]
    public void Cursor_carries_a_value_that_contains_the_separator_because_the_value_is_read_last()
    {
        var cursor = Cursor.For(SortOrder.Ascending("sku_code"), "A|B|C", 7);

        Assert.True(Cursor.TryParse(cursor.Encode(), out var back));
        Assert.Equal(cursor, back);
        Assert.Equal("A|B|C", back.Value);
        Assert.Equal(7, back.Id);
        Assert.Equal("2|sku_code|7|A|B|C", Decode(cursor.Encode()));
    }



    [Fact]
    public void Cursor_for_a_sort_on_the_id_holds_only_the_id()
    {
        var cursor = Cursor.For(ById, 1234567890123);

        Assert.True(Cursor.TryParse(cursor.Encode(), out var back));
        Assert.Equal(cursor, back);
        Assert.Null(back.Value);
        Assert.Equal(1234567890123, back.Id);
        Assert.Equal(ById, back.Sort);
        Assert.Equal("2|id|1234567890123", Decode(cursor.Encode()));
        Assert.Equal("id", Cursor.IdField);
    }



    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not base64 url!")]
    [InlineData("AA")]
    public void Cursor_TryParse_rejects_text_this_api_did_not_issue(string? text)
    {
        var parsed = Cursor.TryParse(text, out var cursor);

        Assert.False(parsed);
        Assert.True(cursor.IsEmpty);
    }



    [Theory]
    [InlineData("42")]
    [InlineData("Created_At|v|42")]
    [InlineData("-|42")]
    [InlineData("id|")]
    [InlineData("id|-1")]
    [InlineData("id|+1")]
    [InlineData("id| 1")]
    [InlineData("id|x")]
    [InlineData("id|1|2")]
    [InlineData("created_at|42")]
    [InlineData("created_at|v|w|42")]
    [InlineData("created_at||42")]
    [InlineData("created_at|42|v\u0001")]
    [InlineData("created_at|42|")]
    [InlineData("created_at|v|")]
    [InlineData("created_at|9223372036854775808|v")]
    public void Cursor_TryParse_rejects_a_payload_whose_shape_does_not_match_its_sort(string payload)
    {
        Assert.False(Cursor.TryParse(Encode("2|" + payload), out _));
    }



    [Theory]
    [InlineData("sku|123|7")]            // the earlier sort|value|id layout with a numeric value: would read as id 123, value 7
    [InlineData("created_at|42|v")]      // the current shape without the layout marker
    [InlineData("1|created_at|42|v")]    // another layout number
    [InlineData("2")]
    [InlineData("2|")]
    [InlineData("2|id")]
    public void Cursor_TryParse_refuses_a_cursor_of_another_layout_instead_of_rereading_it(string payload)
    {
        Assert.False(Cursor.TryParse(Encode(payload), out var cursor));
        Assert.True(cursor.IsEmpty);
    }



    [Fact]
    public void Cursor_For_rejects_no_sort_the_wrong_overload_bad_values_and_negative_ids()
    {
        Assert.Equal("sort", Assert.Throws<ArgumentException>(() => Cursor.For(default, "v", 1)).ParamName);
        Assert.Equal("sort", Assert.Throws<ArgumentException>(() => Cursor.For(default, 1)).ParamName);
        Assert.Equal("sort", Assert.Throws<ArgumentException>(() => Cursor.For(ById, "v", 1)).ParamName);
        Assert.Equal("sort", Assert.Throws<ArgumentException>(() => Cursor.For(NewestFirst, 1)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentException>(() => Cursor.For(NewestFirst, null!, 1)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentException>(() => Cursor.For(NewestFirst, string.Empty, 1)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentException>(() => Cursor.For(NewestFirst, "a\u0001", 1)).ParamName);
        Assert.Equal("id", Assert.Throws<ArgumentException>(() => Cursor.For(NewestFirst, "v", -1)).ParamName);
        Assert.Equal("id", Assert.Throws<ArgumentException>(() => Cursor.For(ById, -1)).ParamName);
    }



    [Fact]
    public void Cursor_equality_is_by_sort_value_and_id_and_the_default_cursor_is_empty()
    {
        Assert.Equal(Cursor.For(NewestFirst, "v", 1), Cursor.For(NewestFirst, "v", 1));
        Assert.NotEqual(Cursor.For(NewestFirst, "v", 1), Cursor.For(NewestFirst, "v", 2));
        Assert.NotEqual(Cursor.For(NewestFirst, "v", 1), Cursor.For(NewestFirst, "w", 1));
        Assert.NotEqual(Cursor.For(SortOrder.Ascending("id"), 1), Cursor.For(SortOrder.Descending("id"), 1));
        Assert.Equal(Cursor.For(NewestFirst, "v", 1).GetHashCode(), Cursor.For(NewestFirst, "v", 1).GetHashCode());
        Assert.Equal(default, default(Cursor));
        Assert.Equal(string.Empty, default(Cursor).Encode());
        Assert.True(default(Cursor).IsEmpty);
        Assert.Null(default(Cursor).Value);
    }



    [Theory]
    [InlineData("created_at", "created_at", SortDirection.Ascending)]
    [InlineData("-created_at", "created_at", SortDirection.Descending)]
    [InlineData("sku2", "sku2", SortDirection.Ascending)]
    public void SortOrder_parses_the_query_parameter_form(string text, string field, SortDirection direction)
    {
        Assert.True(SortOrder.TryParse(text, out var sort));

        Assert.Equal(field, sort.Field);
        Assert.Equal(direction, sort.Direction);
        Assert.Equal(text, sort.ToString());
    }



    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("--created_at")]
    [InlineData("+created_at")]
    [InlineData("Created_At")]
    [InlineData("2nd")]
    [InlineData("_id")]
    [InlineData("created at")]
    [InlineData("id|sku")]
    public void SortOrder_TryParse_rejects_anything_but_a_snake_case_field(string? text)
    {
        Assert.False(SortOrder.TryParse(text, out var sort));
        Assert.Equal(default, sort);
    }



    [Fact]
    public void SortOrder_limits_the_field_length_and_the_default_is_empty()
    {
        Assert.True(SortOrder.TryParse(new string('a', SortOrder.MaxFieldLength), out _));
        Assert.False(SortOrder.TryParse(new string('a', SortOrder.MaxFieldLength + 1), out _));
        Assert.Equal("field", Assert.Throws<ArgumentException>(() => new SortOrder("Bad", SortDirection.Ascending)).ParamName);
        Assert.Equal(string.Empty, default(SortOrder).ToString());
        Assert.Null(default(SortOrder).Field);
    }



    [Fact]
    public void PageSorting_serves_the_default_or_a_declared_field_in_either_direction()
    {
        Assert.True(Sorting.TryResolve(null, out var fallback, out var noError));
        Assert.True(Sorting.TryResolve("sku", out var bySku, out _));
        Assert.True(Sorting.TryResolve("-id", out var byIdDescending, out _));
        Assert.True(Sorting.TryResolve("created_at", out var oldestFirst, out _));

        Assert.Null(noError);
        Assert.Equal(NewestFirst, fallback);
        Assert.Equal(SortOrder.Ascending("sku"), bySku);
        Assert.Equal(SortOrder.Descending("id"), byIdDescending);
        Assert.Equal(SortOrder.Ascending("created_at"), oldestFirst);
        Assert.Equal(["created_at", "id", "sku"], Sorting.Fields);
        Assert.Equal(NewestFirst, Sorting.Default);
    }



    [Theory]
    [InlineData("weight")]
    [InlineData("-weight")]
    [InlineData("Sku")]
    [InlineData("")]
    public void PageSorting_rejects_an_undeclared_or_malformed_sort_and_lists_the_choices(string text)
    {
        Assert.False(Sorting.TryResolve(text, out var sort, out var error));

        Assert.Equal(default, sort);
        Assert.Equal("'sort' must be one of created_at, -created_at, id, -id, sku, -sku.", error);
    }



    [Fact]
    public void PageSorting_requires_a_default_and_valid_fields()
    {
        Assert.Equal("defaultSort", Assert.Throws<ArgumentException>(() => new PageSorting(default)).ParamName);
        Assert.Equal("field", Assert.Throws<ArgumentException>(() => new PageSorting(ById, "Bad Field")).ParamName);
        Assert.Throws<ArgumentNullException>(() => new PageSorting(ById, null!));
        Assert.Equal(["id"], new PageSorting(ById, "id").Fields);
    }



    [Fact]
    public void PageRequest_defaults_to_the_first_page_of_the_default_sort_and_size()
    {
        var ok = new PageRequest().TryResolve(Sorting, out var query, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(query);
        Assert.Equal(PageDirection.Forward, query.Direction);
        Assert.True(query.IsFirstPage);
        Assert.Equal(NewestFirst, query.Sort);
        Assert.Equal(PageRequest.DefaultSize, query.Size);
    }



    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(25, 25)]
    [InlineData(500, 500)]
    [InlineData(9999, 500)]
    public void PageRequest_clamps_the_size(int requested, int expected)
    {
        var request = new PageRequest { Size = requested };

        Assert.Equal(expected, request.EffectiveSize);
        Assert.True(request.TryResolve(Sorting, out var query, out _));
        Assert.Equal(expected, query.Size);
    }



    [Theory]
    [InlineData(10L, null)]
    [InlineData(null, 5L)]
    [InlineData(5L, 5L)]
    public void PageRequest_accepts_a_one_sided_or_single_id_range(long? idFrom, long? idTo)
    {
        Assert.True(new PageRequest { IdFrom = idFrom, IdTo = idTo }.TryResolve(Sorting, out var query, out var error));

        Assert.NotNull(query);
        Assert.Null(error);
    }



    [Fact]
    public void PageRequest_resolves_after_as_forward_and_before_as_backward_in_the_cursor_sort()
    {
        var encoded = Cursor.For(SortOrder.Ascending("sku"), "A-100", 7).Encode();

        Assert.True(new PageRequest { After = encoded, Sort = "sku" }.TryResolve(Sorting, out var forward, out _));
        Assert.True(new PageRequest { Before = encoded, Sort = "sku" }.TryResolve(Sorting, out var backward, out _));

        Assert.Equal(PageDirection.Forward, forward.Direction);
        Assert.Equal(PageDirection.Backward, backward.Direction);
        Assert.Equal(("A-100", 7L), (forward.Cursor.Value, forward.Cursor.Id));
        Assert.False(forward.IsFirstPage);
        Assert.Equal(SortOrder.Ascending("sku"), backward.Sort);
    }



    [Fact]
    public void PageRequest_refuses_a_cursor_issued_under_another_sort()
    {
        var newestFirstCursor = Cursor.For(NewestFirst, "2026-09-19T12:00:00Z", 42).Encode();

        Assert.False(new PageRequest { After = newestFirstCursor, Sort = "created_at" }.TryResolve(Sorting, out var flipped, out var flippedError));
        Assert.False(new PageRequest { Before = newestFirstCursor, Sort = "sku" }.TryResolve(Sorting, out _, out var otherFieldError));
        Assert.True(new PageRequest { After = newestFirstCursor }.TryResolve(Sorting, out _, out _));

        Assert.Null(flipped);
        Assert.Equal("'after' was issued for sort '-created_at', not 'created_at'; after changing the sort, start again from the first page.", flippedError);
        Assert.Equal("'before' was issued for sort '-created_at', not 'sku'; after changing the sort, start again from the first page.", otherFieldError);
    }



    [Fact]
    public void PageRequest_rejects_both_cursors_an_inverted_id_range_a_foreign_cursor_and_an_unknown_sort()
    {
        var encoded = Cursor.For(NewestFirst, "2026-09-19T12:00:00Z", 7).Encode();

        Assert.False(new PageRequest { After = encoded, Before = encoded }.TryResolve(Sorting, out _, out var both));
        Assert.False(new PageRequest { IdFrom = 10, IdTo = 5 }.TryResolve(Sorting, out _, out var range));
        Assert.False(new PageRequest { After = "??" }.TryResolve(Sorting, out _, out var afterError));
        Assert.False(new PageRequest { Before = "??" }.TryResolve(Sorting, out _, out var beforeError));
        Assert.False(new PageRequest { Sort = "weight" }.TryResolve(Sorting, out _, out var sortError));

        Assert.Equal("Send either 'after' or 'before', not both.", both);
        Assert.Equal("'id_from' must not exceed 'id_to'.", range);
        Assert.Equal("'after' is not a cursor this API issued.", afterError);
        Assert.Equal("'before' is not a cursor this API issued.", beforeError);
        Assert.Equal("'sort' must be one of created_at, -created_at, id, -id, sku, -sku.", sortError);
        Assert.Throws<ArgumentNullException>(() => new PageRequest().TryResolve(null!, out _, out _));
    }



    [Theory]
    [InlineData(PageDirection.Forward, SortDirection.Ascending, true)]
    [InlineData(PageDirection.Forward, SortDirection.Descending, false)]
    [InlineData(PageDirection.Backward, SortDirection.Ascending, false)]
    [InlineData(PageDirection.Backward, SortDirection.Descending, true)]
    public void PageQuery_seeks_greater_values_when_the_read_and_the_sort_agree(PageDirection read, SortDirection sort, bool greater)
    {
        var query = new PageQuery(read, Cursor.For(new SortOrder("id", sort), 7), new SortOrder("id", sort), Size: 10);

        Assert.Equal(greater, query.SeeksGreater);
    }



    [Fact]
    public void Page_carries_items_cursors_total_and_id_bounds()
    {
        var page = new Page<string>(Items: ["a", "b"], NextCursor: "next", PreviousCursor: "prev", TotalCount: 12, MinId: 100, MaxId: 111);

        Assert.Equal(["a", "b"], page.Items);
        Assert.Equal("next", page.NextCursor);
        Assert.Equal("prev", page.PreviousCursor);
        Assert.Equal(12, page.TotalCount);
        Assert.Equal(100, page.MinId);
        Assert.Equal(111, page.MaxId);
    }



    [Fact]
    public void Page_Empty_has_nothing_and_Page_rejects_null_items_and_negative_totals()
    {
        var empty = Page.Empty<string>();

        Assert.Empty(empty.Items);
        Assert.Null(empty.NextCursor);
        Assert.Equal(0, empty.TotalCount);
        Assert.Throws<ArgumentNullException>(() => new Page<string>(Items: null!, NextCursor: null, PreviousCursor: null, TotalCount: 0, MinId: null, MaxId: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Page<string>(Items: [], NextCursor: null, PreviousCursor: null, TotalCount: -1, MinId: null, MaxId: null));
    }



    private static string Encode(string payload)
    {
        return System.Buffers.Text.Base64Url.EncodeToString(System.Text.Encoding.UTF8.GetBytes(payload));
    }



    private static string Decode(string encoded)
    {
        return System.Text.Encoding.UTF8.GetString(System.Buffers.Text.Base64Url.DecodeFromChars(encoded));
    }
}
