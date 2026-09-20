// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Identifiers;

namespace Wolfgang.Wms.UnitTests.Identifiers;

public sealed class Gs1Tests
{
    [Theory]
    [InlineData("09501101530003", true)]
    [InlineData("9501101530003", true)]
    [InlineData("012345678905", true)]
    [InlineData("96385074", true)]
    [InlineData("09501101530004", false)]
    [InlineData("0950110153000", false)]
    [InlineData("0950110153000A", false)]
    [InlineData(null, false)]
    public void IsValidGtin_checks_length_and_check_digit(string? gtin, bool expected)
    {
        Assert.Equal(expected, Gs1.IsValidGtin(gtin));
    }



    [Theory]
    [InlineData("106141411234567897", true)]
    [InlineData("106141411234567890", false)]
    [InlineData("10614141123456789", false)]
    public void IsValidSscc_checks_18_digits_and_check_digit(string sscc, bool expected)
    {
        Assert.Equal(expected, Gs1.IsValidSscc(sscc));
    }



    [Fact]
    public void CheckDigit_follows_the_gs1_mod_10_weighting()
    {
        Assert.Equal(3, Gs1.CheckDigit("0950110153000"));
        Assert.Equal(7, Gs1.CheckDigit("10614141123456789"));
        Assert.Throws<ArgumentException>(() => Gs1.CheckDigit(string.Empty));
        Assert.Throws<ArgumentException>(() => Gs1.CheckDigit("12A"));
        Assert.False(Gs1.HasValidCheckDigit("7"));
    }



    [Theory]
    [InlineData("261231", true)]
    [InlineData("260200", true)]
    [InlineData("280229", true)]
    [InlineData("260229", false)]
    [InlineData("261301", false)]
    [InlineData("26123", false)]
    [InlineData("26123x", false)]
    public void IsValidDate_accepts_YYMMDD_with_day_00_as_end_of_month(string date, bool expected)
    {
        Assert.Equal(expected, Gs1.IsValidDate(date));
    }



    [Fact]
    public void TryParse_reads_the_human_readable_form()
    {
        var ok = Gs1.TryParse("(01)09501101530003(17)261231(10)ABC123(21)SN-7", out var elements, out var error);

        Assert.True(ok, error);
        Assert.Equal(["(01)09501101530003", "(17)261231", "(10)ABC123", "(21)SN-7"], elements.Select(e => e.ToString()));
        Assert.Equal("GTIN", elements[0].ApplicationIdentifier.Description);
    }



    [Fact]
    public void TryParse_reads_the_scanned_form_with_group_separators_and_a_symbology_identifier()
    {
        var scanned = "]C1" + "0109501101530003" + "17261231" + "10ABC123" + Gs1.GroupSeparator + "21SN-7" + Gs1.GroupSeparator + "3103001250";

        var ok = Gs1.TryParse(scanned, out var elements, out var error);

        Assert.True(ok, error);
        Assert.Equal(["01", "17", "10", "21", "3103"], elements.Select(e => e.ApplicationIdentifier.Code));
        Assert.Equal("001250", elements[4].Value);
    }



    [Theory]
    [InlineData("(01)09501101530004", "(01) GTIN: check digit is wrong.")]
    [InlineData("(01)0950110153", "(01) GTIN: expected 14 characters, got 10.")]
    [InlineData("(01)0950110153000A", "(01) GTIN: digits only.")]
    [InlineData("(17)261331", "(17) Expiry date: not a YYMMDD date.")]
    [InlineData("(37)12x", "(37) Count of trade items: digits only.")]
    [InlineData("(10)ABCDEFGHIJKLMNOPQRSTU", "(10) Batch or lot: expected 1 to 20 characters, got 21.")]
    [InlineData("(10)", "(10) Batch or lot: expected 1 to 20 characters, got 0.")]
    [InlineData("(99999)X", "Unknown application identifier '99999'.")]
    [InlineData("(01", "Expected '(AI)' at '(01'.")]
    [InlineData("5599", "Unknown application identifier at '5599'.")]
    [InlineData("010950110153000A", "(01) GTIN: digits only.")]
    [InlineData("10ABCDEFGHIJKLMNOPQRSTU", "(10) Batch or lot: expected 1 to 20 characters, got 21.")]
    [InlineData("", "An element string is required.")]
    public void TryParse_names_the_ai_and_the_broken_rule(string text, string expectedError)
    {
        var ok = Gs1.TryParse(text, out var elements, out var error);

        Assert.False(ok);
        Assert.Empty(elements);
        Assert.Equal(expectedError, error);
    }



    [Fact]
    public void TryParse_rejects_a_control_character_inside_a_value_and_an_empty_scan()
    {
        Assert.False(Gs1.TryParse("(10)ABC", out _, out var controlError));
        Assert.False(Gs1.TryParse(Gs1.GroupSeparator.ToString(), out _, out var emptyError));

        Assert.Equal("(10) Batch or lot: control characters are not allowed.", controlError);
        Assert.Equal("No application identifiers found.", emptyError);
    }



    [Fact]
    public void The_table_knows_the_warehouse_ais_and_finds_by_longest_code()
    {
        Assert.Equal("3103", Gs1ApplicationIdentifier.Find("3103001250")!.Code);
        Assert.Equal("01", Gs1ApplicationIdentifier.Find("01095")!.Code);
        Assert.Null(Gs1ApplicationIdentifier.Find("5"));
        Assert.Contains(Gs1ApplicationIdentifier.Known, ai => string.Equals(ai.Code, "00", StringComparison.Ordinal) && ai.IsFixedLength && ai.Rule == Gs1ValueRule.CheckDigit);
        Assert.Contains(Gs1ApplicationIdentifier.Known, ai => string.Equals(ai.Code, "95", StringComparison.Ordinal) && !ai.IsFixedLength && ai.MaxLength == 90);
    }
}
