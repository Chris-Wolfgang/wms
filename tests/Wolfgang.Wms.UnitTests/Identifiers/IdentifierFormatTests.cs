// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics;
using Wolfgang.Wms.Domain.Identifiers;

namespace Wolfgang.Wms.UnitTests.Identifiers;

public sealed class IdentifierFormatTests
{
    [Theory]
    [InlineData("AAA-999", "^[A-Za-z]{3}-[0-9]{3}$")]
    [InlineData("9(8)", "^[0-9]{8}$")]
    [InlineData("T-X(6)", "^T-[A-Za-z0-9]{6}$")]
    [InlineData("?(3).A", "^.{3}\\.[A-Za-z]$")]
    [InlineData("LOT 9", "^LOT\\ [0-9]$")]
    public void Masks_compile_to_anchored_regular_expressions(string mask, string expected)
    {
        Assert.Equal(expected, MaskCompiler.ToRegex(mask));
        Assert.Equal(expected, IdentifierFormat.Create(FormatKind.Mask, mask).Pattern);
    }



    [Theory]
    [InlineData("AAA-999", "ABC-123", true)]
    [InlineData("AAA-999", "AB-123", false)]
    [InlineData("AAA-999", "abc-123", true)]
    [InlineData("9(8)", "12345678", true)]
    [InlineData("9(8)", "1234567", false)]
    [InlineData("9(8)", "123456789", false)]
    [InlineData("T-X(6)", "T-A1B2C3", true)]
    [InlineData("T-X(6)", "T-A1B2C", false)]
    public void Mask_formats_match_whole_values_only(string mask, string value, bool expected)
    {
        var format = IdentifierFormat.Create(FormatKind.Mask, mask);

        Assert.Equal(expected, format.IsMatch(value));
        Assert.Equal(FormatKind.Mask, format.Kind);
        Assert.Equal(mask, format.Source);
    }



    [Theory]
    [InlineData("(n)")]
    [InlineData("A(0)")]
    [InlineData("A(x)")]
    [InlineData("A(3")]
    [InlineData("  ")]
    public void Invalid_masks_are_rejected(string mask)
    {
        Assert.Throws<ArgumentException>(() => MaskCompiler.ToRegex(mask));
    }



    [Theory]
    [InlineData("[A-Z]{2}\\d+", "AB12", true)]
    [InlineData("[A-Z]{2}\\d+", "xAB12", false)]
    [InlineData("^[A-Z]{2}\\d+$", "AB12", true)]
    [InlineData("ab|cd", "cd", true)]
    [InlineData("ab|cd", "abcd", false)]
    public void Regex_formats_are_anchored_to_the_whole_value(string regex, string value, bool expected)
    {
        var format = IdentifierFormat.Create(FormatKind.Regex, regex);

        Assert.Equal(expected, format.IsMatch(value));
        Assert.StartsWith("^(?:", format.Pattern, StringComparison.Ordinal);
        Assert.EndsWith(")$", format.Pattern, StringComparison.Ordinal);
    }



    [Fact]
    public void Patterns_use_the_non_backtracking_engine_when_they_can_and_a_timeout_otherwise()
    {
        var linear = IdentifierFormat.Create(FormatKind.Regex, "(a+)+b");
        var backtracking = IdentifierFormat.Create(FormatKind.Regex, "(a)\\1");

        Assert.True(linear.IsNonBacktracking);
        Assert.False(backtracking.IsNonBacktracking);
        Assert.True(backtracking.IsMatch("aa"));
        Assert.Equal(TimeSpan.FromMilliseconds(100), IdentifierFormat.MatchTimeout);
    }



    [Fact]
    public void A_catastrophic_pattern_cannot_stall_a_scan()
    {
        // The backreference forces the backtracking engine; the nested quantifier makes it exponential.
        var format = IdentifierFormat.Create(FormatKind.Regex, "(a+)+\\1b");
        var input = new string('a', 40) + "c";
        var stopwatch = Stopwatch.StartNew();

        var matched = format.IsMatch(input);

        stopwatch.Stop();
        Assert.False(format.IsNonBacktracking);
        Assert.False(matched);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"took {stopwatch.Elapsed}");
    }



    [Fact]
    public void TryCreate_reports_why_a_format_is_invalid()
    {
        Assert.False(IdentifierFormat.TryCreate(FormatKind.Regex, "[unclosed", out var none, out var regexError));
        Assert.False(IdentifierFormat.TryCreate(FormatKind.Mask, "A(0)", out _, out var maskError));
        Assert.False(IdentifierFormat.TryCreate(FormatKind.Mask, "", out _, out var blankError));
        Assert.True(IdentifierFormat.TryCreate(FormatKind.Mask, "AA", out var ok, out var noError));

        Assert.Null(none);
        Assert.Contains("not a valid regular expression", regexError, StringComparison.Ordinal);
        Assert.Contains("'(n)' must follow a token", maskError, StringComparison.Ordinal);
        Assert.Equal("A format is required.", blankError);
        Assert.NotNull(ok);
        Assert.Null(noError);
    }



    [Fact]
    public void Create_rejects_blank_sources_unknown_kinds_and_IsMatch_rejects_null()
    {
        Assert.Throws<ArgumentException>(() => IdentifierFormat.Create(FormatKind.Regex, " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdentifierFormat.Create((FormatKind)9, "A"));
        Assert.Throws<ArgumentNullException>(() => IdentifierFormat.Create(FormatKind.Mask, "A").IsMatch(null!));
    }
}
