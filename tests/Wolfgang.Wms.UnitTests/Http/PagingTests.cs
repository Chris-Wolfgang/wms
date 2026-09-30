// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Http.Paging;

namespace Wolfgang.Wms.UnitTests.Http;

public sealed class PagingTests
{
    private static readonly SortOrder ById = SortOrder.Ascending("id");

    private static readonly SortOrder NewestFirst = SortOrder.Descending("created_at");

    private static readonly PageSorting Sorting = new(NewestFirst, "id", "sku");



    [Fact]
    public void Cursor_round_trips_its_sort_and_keys_through_an_opaque_url_safe_string()
    {
        var cursor = Cursor.For(NewestFirst, "2026-09-19T12:00:00Z", "42");

        var encoded = cursor.Encode();
        var parsed = Cursor.TryParse(encoded, out var back);

        Assert.True(parsed);
        Assert.Equal(cursor, back);
        Assert.Equal(NewestFirst, back.Sort);
        Assert.Equal(["2026-09-19T12:00:00Z", "42"], back.Keys);
        Assert.Equal(encoded, cursor.ToString());
        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
        Assert.DoesNotContain('=', encoded);
    }



    [Fact]
    public void Cursor_for_an_id_uses_the_invariant_representation()
    {
        var cursor = Cursor.For(ById, 1234567890123);

        Assert.Equal(["1234567890123"], cursor.Keys);
        Assert.Equal(ById, cursor.Sort);
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
        Assert.Empty(cursor.Keys);
    }



    [Theory]
    [InlineData("42")]
    [InlineData("Created_At|42")]
    [InlineData("-|42")]
    [InlineData("id|")]
    [InlineData("id|4\u00012")]
    public void Cursor_TryParse_rejects_a_payload_without_a_valid_sort_and_keys(string payload)
    {
        var text = System.Buffers.Text.Base64Url.EncodeToString(System.Text.Encoding.UTF8.GetBytes(payload));

        Assert.False(Cursor.TryParse(text, out _));
    }



    [Fact]
    public void Cursor_For_rejects_no_sort_no_keys_null_keys_and_the_separator()
    {
        Assert.Equal("sort", Assert.Throws<ArgumentException>(() => Cursor.For(default, "1")).ParamName);
        Assert.Equal("keys", Assert.Throws<ArgumentException>(() => Cursor.For(ById)).ParamName);
        Assert.Equal("keys", Assert.Throws<ArgumentException>(() => Cursor.For(ById, (string)null!)).ParamName);
        Assert.Equal("keys", Assert.Throws<ArgumentException>(() => Cursor.For(ById, "a|b")).ParamName);
        Assert.Throws<ArgumentNullException>(() => Cursor.For(ById, (string[])null!));
    }



    [Fact]
    public void Cursor_equality_is_by_sort_and_keys_and_the_default_cursor_is_empty()
    {
        Assert.Equal(Cursor.For(ById, "1"), Cursor.For(ById, "1"));
        Assert.NotEqual(Cursor.For(ById, "1"), Cursor.For(ById, "2"));
        Assert.NotEqual(Cursor.For(SortOrder.Ascending("id"), "1"), Cursor.For(SortOrder.Descending("id"), "1"));
        Assert.Equal(Cursor.For(ById, "1").GetHashCode(), Cursor.For(ById, "1").GetHashCode());
        Assert.Equal(default, default(Cursor));
        Assert.Equal(string.Empty, default(Cursor).Encode());
        Assert.Empty(default(Cursor).Keys);
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
        var encoded = Cursor.For(SortOrder.Ascending("sku"), "A-100", "7").Encode();

        Assert.True(new PageRequest { After = encoded, Sort = "sku" }.TryResolve(Sorting, out var forward, out _));
        Assert.True(new PageRequest { Before = encoded, Sort = "sku" }.TryResolve(Sorting, out var backward, out _));

        Assert.Equal(PageDirection.Forward, forward.Direction);
        Assert.Equal(PageDirection.Backward, backward.Direction);
        Assert.Equal(["A-100", "7"], forward.Cursor.Keys);
        Assert.False(forward.IsFirstPage);
        Assert.Equal(SortOrder.Ascending("sku"), backward.Sort);
    }



    [Fact]
    public void PageRequest_refuses_a_cursor_issued_under_another_sort()
    {
        var newestFirstCursor = Cursor.For(NewestFirst, "2026-09-19T12:00:00Z", "42").Encode();

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
        var encoded = Cursor.For(NewestFirst, "2026-09-19T12:00:00Z", "7").Encode();

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
}
