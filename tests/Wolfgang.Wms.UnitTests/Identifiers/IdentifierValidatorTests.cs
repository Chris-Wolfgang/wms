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
}
