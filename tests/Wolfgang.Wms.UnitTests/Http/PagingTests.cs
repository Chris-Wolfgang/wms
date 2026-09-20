// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Http.Paging;

namespace Wolfgang.Wms.UnitTests.Http;

public sealed class PagingTests
{
    [Fact]
    public void Cursor_round_trips_its_keys_through_an_opaque_url_safe_string()
    {
        var cursor = Cursor.For("2026-09-19T12:00:00Z", "42");

        var encoded = cursor.Encode();
        var parsed = Cursor.TryParse(encoded, out var back);

        Assert.True(parsed);
        Assert.Equal(cursor, back);
        Assert.Equal(["2026-09-19T12:00:00Z", "42"], back.Keys);
        Assert.Equal(encoded, cursor.ToString());
        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
        Assert.DoesNotContain('=', encoded);
    }



    [Fact]
    public void Cursor_for_an_id_uses_the_invariant_representation()
    {
        Assert.Equal(["1234567890123"], Cursor.For(1234567890123).Keys);
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



    [Fact]
    public void Cursor_For_rejects_no_keys_null_keys_and_the_separator()
    {
        Assert.Throws<ArgumentException>(() => Cursor.For());
        Assert.Throws<ArgumentException>(() => Cursor.For(new string[] { null! }));
        Assert.Throws<ArgumentException>(() => Cursor.For("a|b"));
        Assert.Throws<ArgumentNullException>(() => Cursor.For((string[])null!));
    }



    [Fact]
    public void Cursor_equality_is_by_keys_and_the_default_cursor_is_empty()
    {
        Assert.Equal(Cursor.For("1"), Cursor.For("1"));
        Assert.NotEqual(Cursor.For("1"), Cursor.For("2"));
        Assert.Equal(Cursor.For("1").GetHashCode(), Cursor.For("1").GetHashCode());
        Assert.Equal(default, default(Cursor));
        Assert.Equal(string.Empty, default(Cursor).Encode());
    }



    [Fact]
    public void PageRequest_defaults_to_the_first_page_of_the_default_size()
    {
        var request = new PageRequest();

        var ok = request.TryResolve(out var direction, out var cursor, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(PageDirection.Forward, direction);
        Assert.Empty(cursor.Keys);
        Assert.Equal(PageRequest.DefaultSize, request.EffectiveSize);
    }



    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(25, 25)]
    [InlineData(500, 500)]
    [InlineData(9999, 500)]
    public void PageRequest_clamps_the_size(int requested, int expected)
    {
        Assert.Equal(expected, new PageRequest { Size = requested }.EffectiveSize);
    }



    [Fact]
    public void PageRequest_resolves_after_as_forward_and_before_as_backward()
    {
        var encoded = Cursor.For(7).Encode();

        Assert.True(new PageRequest { After = encoded }.TryResolve(out var forward, out var afterCursor, out _));
        Assert.True(new PageRequest { Before = encoded }.TryResolve(out var backward, out var beforeCursor, out _));

        Assert.Equal(PageDirection.Forward, forward);
        Assert.Equal(PageDirection.Backward, backward);
        Assert.Equal(Cursor.For(7), afterCursor);
        Assert.Equal(Cursor.For(7), beforeCursor);
    }



    [Fact]
    public void PageRequest_rejects_both_cursors_an_inverted_id_range_and_a_foreign_cursor()
    {
        var encoded = Cursor.For(7).Encode();

        Assert.False(new PageRequest { After = encoded, Before = encoded }.TryResolve(out _, out _, out var both));
        Assert.False(new PageRequest { IdFrom = 10, IdTo = 5 }.TryResolve(out _, out _, out var range));
        Assert.False(new PageRequest { After = "??" }.TryResolve(out _, out _, out var afterError));
        Assert.False(new PageRequest { Before = "??" }.TryResolve(out _, out _, out var beforeError));

        Assert.Equal("Send either 'after' or 'before', not both.", both);
        Assert.Equal("'id_from' must not exceed 'id_to'.", range);
        Assert.Equal("'after' is not a cursor this API issued.", afterError);
        Assert.Equal("'before' is not a cursor this API issued.", beforeError);
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
