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
    [InlineData("261300", false)]
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
    public void TryParse_when_the_prefix_is_not_a_GS1_symbology_identifier_does_not_strip_it()
    {
        var ok = Gs1.TryParse("]X0" + "0109501101530003", out var elements, out var error);

        Assert.False(ok);
        Assert.Empty(elements);
        Assert.Equal("Unknown application identifier at ']X00'.", error);
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



    [Theory]
    [InlineData("(10)ABC\t")]
    [InlineData("(10)ABC\n")]
    [InlineData("(10)ABC\r\n")]
    [InlineData("10ABC\r")]
    [InlineData("\t(10)ABC")]
    public void TryParse_when_the_string_has_a_surrounding_tab_or_line_break_rejects_it(string text)
    {
        var ok = Gs1.TryParse(text, out var elements, out var error);

        Assert.False(ok, "a control character was trimmed away and the string accepted");
        Assert.Empty(elements);
        Assert.NotNull(error);
    }



    [Theory]
    [InlineData("(21)SN-7 ", "SN-7 ")]
    [InlineData("21SN-7 ", "SN-7 ")]
    [InlineData("(10) ABC", " ABC")]
    [InlineData("(01)09501101530003(10)ABC ", "ABC ")]
    public void TryParse_when_a_value_has_leading_or_trailing_spaces_keeps_them(string text, string expectedLastValue)
    {
        var ok = Gs1.TryParse(text, out var elements, out var error);

        Assert.True(ok, error);
        Assert.Equal
        (
            expectedLastValue,
            elements[^1].Value
        );
    }



    [Theory]
    [InlineData("(421)84019103", true)]
    [InlineData("(421)276D-80331", true)]
    [InlineData("(421)8401", true)]
    [InlineData("(421)ABC", false)]
    [InlineData("(421)ABC12345", false)]
    [InlineData("(421)99912345", false)]
    [InlineData("(421)840", false)]
    public void TryParse_when_ai_is_421_requires_an_iso_3166_country_code_and_a_postal_code(string text, bool expected)
    {
        var ok = Gs1.TryParse(text, out _, out var error);

        Assert.Equal(expected, ok);
        if (!expected)
        {
            Assert.Equal
            (
                "(421) Ship-to postal code with country: expected a 3-digit ISO 3166 country code and 1 to 9 more characters.",
                error
            );
        }
    }



    [Theory]
    [InlineData("(7003)2612312359", null)]
    [InlineData("(7003)2612310000", null)]
    [InlineData("(7003)2802291200", null)]
    [InlineData("(7003)9913999999", "(7003) Expiry date and time: not a YYMMDDhhmm date and time.")]
    [InlineData("(7003)2612002359", "(7003) Expiry date and time: not a YYMMDDhhmm date and time.")]
    [InlineData("(7003)2612312400", "(7003) Expiry date and time: not a YYMMDDhhmm date and time.")]
    [InlineData("(7003)2612312360", "(7003) Expiry date and time: not a YYMMDDhhmm date and time.")]
    [InlineData("(7003)2602291200", "(7003) Expiry date and time: not a YYMMDDhhmm date and time.")]
    [InlineData("(7003)261231235", "(7003) Expiry date and time: expected 10 characters, got 9.")]
    public void TryParse_when_ai_is_7003_requires_a_real_YYMMDDhhmm(string text, string? expectedError)
    {
        var ok = Gs1.TryParse(text, out _, out var error);

        Assert.Equal(expectedError is null, ok);
        Assert.Equal(expectedError, error);
    }



    [Theory]
    [InlineData("2612312359", true)]
    [InlineData("2612312400", false)]
    [InlineData("2612002359", false)]
    [InlineData("261231235x", false)]
    [InlineData("261231", false)]
    [InlineData(null, false)]
    public void IsValidDateTime_when_given_a_value_checks_length_digits_date_and_time(string? value, bool expected)
    {
        Assert.Equal(expected, Gs1.IsValidDateTime(value));
    }



    [Fact]
    public void TryParse_when_ai_is_7001_requires_exactly_13_digits()
    {
        Assert.True(Gs1.TryParse("(7001)1234567890123(10)ABC", out var bracketed, out var bracketedError), bracketedError);
        Assert.True(Gs1.TryParse("7001123456789012310ABC", out var runTogether, out var runTogetherError), runTogetherError);
        Assert.True(Gs1.TryParse("70011234567890123" + Gs1.GroupSeparator + "10ABC", out var separated, out var separatedError), separatedError);
        Assert.False(Gs1.TryParse("(7001)123456789012", out _, out var shortError));

        Assert.Equal(["1234567890123", "ABC"], bracketed.Select(e => e.Value));
        Assert.Equal(["1234567890123", "ABC"], runTogether.Select(e => e.Value));
        Assert.Equal(["1234567890123", "ABC"], separated.Select(e => e.Value));
        Assert.Equal("(7001) NATO stock number: expected 13 characters, got 12.", shortError);
    }



    [Fact]
    public void TryParse_when_ai_is_7004_accepts_1_to_4_digits_as_GS1_defines_it_N__4()
    {
        Assert.True(Gs1.TryParse("(7004)12", out var elements, out var error), error);
        Assert.False(Gs1.TryParse("(7004)12345", out _, out var longError));

        Assert.Equal("12", elements[0].Value);
        Assert.Equal("(7004) Active potency: expected 1 to 4 characters, got 5.", longError);
    }



    [Theory]
    [InlineData("]C1010950110153000", true)]
    [InlineData("]e00109501101530003", true)]
    [InlineData("]d20109501101530003", true)]
    [InlineData("]Q30109501101530003", true)]
    [InlineData("]J10109501101530003", true)]
    [InlineData("]C0ABC", false)]
    [InlineData("]Q1ABC", false)]
    [InlineData("]C", false)]
    [InlineData("(01)09501101530003", false)]
    [InlineData(null, false)]
    public void HasGs1SymbologyIdentifier_when_given_a_value_recognises_only_GS1_symbologies(string? value, bool expected)
    {
        Assert.Equal(expected, Gs1.HasGs1SymbologyIdentifier(value));
    }
}
