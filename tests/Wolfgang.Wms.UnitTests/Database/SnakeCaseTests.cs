// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Infrastructure.Database.Conventions;

namespace Wolfgang.Wms.UnitTests.Database;

public sealed class SnakeCaseTests
{
    [Theory]
    [InlineData("Id", "id")]
    [InlineData("ZoneGroupId", "zone_group_id")]
    [InlineData("ErpReleaseId", "erp_release_id")]
    [InlineData("SKUCode", "sku_code")]
    [InlineData("GS1Barcode", "gs1_barcode")]
    [InlineData("Line2Qty", "line2_qty")]
    [InlineData("already_snake", "already_snake")]
    [InlineData("With Space", "with_space")]
    [InlineData("Trailing_", "trailing")]
    [InlineData("__Leading", "leading")]
    public void Of_converts_clr_identifiers(string input, string expected)
    {
        Assert.Equal(expected, SnakeCase.Of(input));
    }



    [Theory]
    [InlineData("id", true)]
    [InlineData("zone_group_id", true)]
    [InlineData("gs1_barcode", true)]
    [InlineData("ZoneGroup", false)]
    [InlineData("zone__group", false)]
    [InlineData("_zone", false)]
    [InlineData("zone_", false)]
    [InlineData("1zone", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Is_accepts_only_snake_case(string? identifier, bool expected)
    {
        Assert.Equal(expected, SnakeCase.Is(identifier));
    }



    [Fact]
    public void Of_rejects_blank_input()
    {
        Assert.Throws<ArgumentException>(() => SnakeCase.Of(" "));
    }
}
