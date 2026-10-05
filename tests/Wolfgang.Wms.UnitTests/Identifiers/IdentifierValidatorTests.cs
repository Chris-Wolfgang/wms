// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Identifiers;

namespace Wolfgang.Wms.UnitTests.Identifiers;

public sealed class IdentifierValidatorTests
{
    private static readonly ValidationProfile Default = new("tote_barcode", SystemMaxLength: 40);



    [Fact]
    public void The_default_profile_stores_the_value_trimmed_and_case_sensitive()
    {
        var result = IdentifierValidator.Validate(Default, "  Tote-0017 ");

        Assert.True(result.IsValid);
        Assert.Equal("Tote-0017", result.Value);
        Assert.Equal("tote_barcode", result.Field);
        Assert.Equal(string.Empty, result.Message);
        Assert.Equal(40, Default.EffectiveMaxLength);
    }



    [Fact]
    public void Control_characters_are_always_rejected()
    {
        var result = IdentifierValidator.Validate(Default, "TOTE");

        Assert.False(result.IsValid);
        Assert.Equal("control_characters", result.FailedRule);
        Assert.Equal("tote_barcode: no control characters", result.Message);
    }



    [Fact]
    public void Unicode_is_accepted_as_received()
    {
        Assert.Equal("Zürich-Ω-日本", IdentifierValidator.Validate(Default, "Zürich-Ω-日本").Value);
    }



    [Fact]
    public void Required_and_length_rules_name_the_rule_and_the_expectation()
    {
        var profile = Default with { Required = true, MinLength = 3, MaxLength = 5 };

        var blank = IdentifierValidator.Validate(profile, "   ");
        var tooShort = IdentifierValidator.Validate(profile, "AB");
        var tooLong = IdentifierValidator.Validate(profile, "ABCDEF");
        var ok = IdentifierValidator.Validate(profile, "ABCD");

        Assert.Equal(("required", "a value is required"), (blank.FailedRule, blank.Expected));
        Assert.Equal(("min_length", "at least 3 characters"), (tooShort.FailedRule, tooShort.Expected));
        Assert.Equal(("max_length", "at most 5 characters"), (tooLong.FailedRule, tooLong.Expected));
        Assert.True(ok.IsValid);
    }



    [Fact]
    public void A_blank_optional_value_is_valid_and_empty()
    {
        var result = IdentifierValidator.Validate(Default, null);

        Assert.True(result.IsValid);
        Assert.Equal(string.Empty, result.Value);
    }



    [Fact]
    public void MaxLength_never_exceeds_the_system_cap()
    {
        var profile = Default with { MaxLength = 400 };

        Assert.Equal(40, profile.EffectiveMaxLength);
        Assert.Equal("max_length", IdentifierValidator.Validate(profile, new string('x', 41)).FailedRule);
    }



    [Theory]
    [InlineData(CaseHandling.MakeUpper, "tote-a", "TOTE-A")]
    [InlineData(CaseHandling.MakeLower, "TOTE-A", "tote-a")]
    [InlineData(CaseHandling.NoChange, "ToTe-A", "ToTe-A")]
    public void Case_handling_runs_before_the_checks(CaseHandling handling, string raw, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var profile = Default with { Case = handling, Format = IdentifierFormat.Create(FormatKind.Regex, expected.Replace("-", "\\-", StringComparison.Ordinal)) };

        var result = IdentifierValidator.Validate(profile, raw);

        Assert.True(result.IsValid, result.Message);
        Assert.Equal(expected, result.Value);
    }



    [Fact]
    public void Trimming_is_configurable_per_side()
    {
        var keepLeading = Default with { TrimLeading = false };
        var keepTrailing = Default with { TrimTrailing = false };

        Assert.Equal("  A", IdentifierValidator.Normalize(keepLeading, "  A  "));
        Assert.Equal("A  ", IdentifierValidator.Normalize(keepTrailing, "  A  "));
    }



    [Fact]
    public void A_format_mismatch_names_the_format()
    {
        var profile = Default with { Format = IdentifierFormat.Create(FormatKind.Mask, "T-9(4)") };

        var bad = IdentifierValidator.Validate(profile, "T-12A4");
        var good = IdentifierValidator.Validate(profile, "T-1234");

        Assert.Equal("format", bad.FailedRule);
        Assert.Equal("tote_barcode: format T-9(4)", bad.Message);
        Assert.True(good.IsValid);
    }



    [Fact]
    public void Profiles_require_a_field_and_a_positive_cap_and_the_validator_rejects_null()
    {
        Assert.Throws<ArgumentException>(() => new ValidationProfile(" ", 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ValidationProfile("f", 0));
        Assert.Throws<ArgumentNullException>(() => IdentifierValidator.Validate(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => IdentifierValidator.Normalize(null!, "x"));
    }



    [Fact]
    public void Profile_when_a_length_is_negative_or_zero_max_throws_even_through_with()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Default with { MaxLength = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => Default with { MaxLength = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => Default with { MinLength = -1 });
        Assert.Null((Default with { MaxLength = null }).MaxLength);
    }



    [Fact]
    public void Validate_when_MinLength_is_above_the_effective_maximum_refuses_the_profile()
    {
        var contradictory = Default with { MinLength = 10, MaxLength = 5 };
        var aboveTheCap = Default with { MinLength = 41 };

        var exception = Assert.Throws<ArgumentException>(() => IdentifierValidator.Validate(contradictory, "ABCDEFG"));

        Assert.Equal("tote_barcode: the minimum length 10 is above the maximum length 5.", contradictory.ConfigurationError);
        Assert.Equal("tote_barcode: the minimum length 41 is above the maximum length 40.", aboveTheCap.ConfigurationError);
        Assert.Null((Default with { MinLength = 5, MaxLength = 5 }).ConfigurationError);
        Assert.Equal("profile", exception.ParamName);
        Assert.StartsWith(contradictory.ConfigurationError!, exception.Message, StringComparison.Ordinal);
    }



    [Theory]
    [InlineData("(01)09501101530004", "(01) GTIN: check digit is wrong.")]
    [InlineData("(01)0950110153000(10)ABC", "(01) GTIN: digits only.")]
    [InlineData("(7003)9913999999", "(7003) Expiry date and time: not a YYMMDDhhmm date and time.")]
    [InlineData("TOTE-17", "Unknown application identifier at 'TOTE'.")]
    public void Validate_when_the_field_holds_GS1_element_strings_surfaces_the_structural_error(string raw, string expected)
    {
        var profile = Default with { Gs1ElementString = true };

        var result = IdentifierValidator.Validate(profile, raw);

        Assert.Equal(("gs1", expected), (result.FailedRule, result.Expected));
        Assert.Equal("tote_barcode: " + expected, result.Message);
    }



    [Fact]
    public void Validate_when_the_field_holds_GS1_element_strings_accepts_both_forms_and_still_applies_length()
    {
        var profile = Default with { Gs1ElementString = true, MaxLength = 32 };
        var scanned = "]C1" + "0109501101530003" + "10ABC" + Gs1.GroupSeparator + "21SN-7";

        var bracketed = IdentifierValidator.Validate(profile, "(01)09501101530003(10)ABC");
        var separated = IdentifierValidator.Validate(profile, scanned);
        var tooLong = IdentifierValidator.Validate(profile, "(01)09501101530003(10)ABCDEFGHIJKL");

        Assert.True(bracketed.IsValid, bracketed.Message);
        Assert.True(separated.IsValid, separated.Message);
        Assert.Equal(scanned, separated.Value);
        Assert.Equal("max_length", tooLong.FailedRule);
    }



    [Fact]
    public void Validate_when_a_value_carries_a_GS1_symbology_identifier_parses_it_in_any_field()
    {
        var broken = IdentifierValidator.Validate(Default, "]C1" + "0109501101530004");
        var good = IdentifierValidator.Validate(Default, "]C1" + "0109501101530003" + "10ABC" + Gs1.GroupSeparator + "21SN-7");
        var plain = IdentifierValidator.Validate(Default, "(01)09501101530004");

        Assert.Equal(("gs1", "(01) GTIN: check digit is wrong."), (broken.FailedRule, broken.Expected));
        Assert.True(good.IsValid, good.Message);
        Assert.True(plain.IsValid, "a field not declared GS1 stores bracketed text as received");
    }
}
