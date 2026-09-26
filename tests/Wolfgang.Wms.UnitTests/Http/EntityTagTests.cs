// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Http;

namespace Wolfgang.Wms.UnitTests.Http;

public sealed class EntityTagTests
{
    [Fact]
    public void FromRowVersion_formats_the_version_as_a_quoted_hex_strong_tag()
    {
        var tag = EntityTag.FromRowVersion(0x1A2B);

        Assert.Equal("\"1a2b\"", tag.Value);
        Assert.Equal("\"1a2b\"", tag.ToString());
    }



    [Fact]
    public void FromCollection_combines_max_version_and_count_so_a_delete_changes_the_tag()
    {
        var before = EntityTag.FromCollection(0x10, 3);
        var afterDelete = EntityTag.FromCollection(0x10, 2);

        Assert.Equal("\"10-3\"", before.Value);
        Assert.NotEqual(before, afterDelete);
    }



    [Fact]
    public void FromCollection_when_count_is_negative_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EntityTag.FromCollection(1, -1));
    }



    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("*", true)]
    [InlineData("\"1a2b\"", true)]
    [InlineData("W/\"1a2b\"", true)]
    [InlineData("\"other\", \"1a2b\"", true)]
    [InlineData("\"other\",W/\"1a2b\" ", true)]
    [InlineData("\"other\"", false)]
    [InlineData("1a2b", false)]
    public void IsMatchedBy_applies_weak_comparison_over_a_comma_separated_header(string? header, bool expected)
    {
        var tag = EntityTag.FromRowVersion(0x1A2B);

        Assert.Equal(expected, tag.IsMatchedBy(header));
    }



    [Fact]
    public void Tags_with_the_same_version_are_equal()
    {
        Assert.Equal(EntityTag.FromRowVersion(7), EntityTag.FromRowVersion(7));
        Assert.NotEqual(EntityTag.FromRowVersion(7), EntityTag.FromRowVersion(8));
    }
}
