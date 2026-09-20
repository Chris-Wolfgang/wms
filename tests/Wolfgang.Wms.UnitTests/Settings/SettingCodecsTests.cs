// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.UnitTests.Settings;

/// <summary>
/// E6.1/E6.3: one stored format per kind, invariant culture, round-tripping on every built-in type; enums
/// by name with their choices; JSON through the caller's serializer; unsupported types refused.
/// </summary>
public sealed class SettingCodecsTests
{
    public enum Sample
    {
        First,
        Second,
    }



    [Fact]
    public void Built_in_codecs_round_trip_with_invariant_culture()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            AssertRoundTrip(true, "true", SettingKind.Boolean);
            AssertRoundTrip(42, "42", SettingKind.Integer);
            AssertRoundTrip(-9_000_000_000L, "-9000000000", SettingKind.Integer);
            AssertRoundTrip(1.5m, "1.5", SettingKind.Number);
            AssertRoundTrip(0.25, "0.25", SettingKind.Number);
            AssertRoundTrip("Hello, Welt", "Hello, Welt", SettingKind.String);
            AssertRoundTrip(TimeSpan.FromMinutes(90), "01:30:00", SettingKind.Duration);
            AssertRoundTrip(new DateTimeOffset(2026, 9, 20, 8, 30, 0, TimeSpan.FromHours(2)), "2026-09-20T08:30:00.0000000+02:00", SettingKind.Timestamp);
            AssertRoundTrip(new SecretText("hunter2"), "hunter2", SettingKind.Secret);
            AssertRoundTrip(Sample.Second, "Second", SettingKind.Enum);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }



    [Theory]
    [InlineData("yes")]
    [InlineData("")]
    [InlineData("1,5")]
    public void Invalid_text_is_rejected_not_thrown(string text)
    {
        Assert.False(SettingCodecs.For<bool>().TryParse(text, out _));
        Assert.False(SettingCodecs.For<int>().TryParse(text, out _));
        Assert.False(SettingCodecs.For<decimal>().TryParse(text, out _));
        Assert.False(SettingCodecs.For<TimeSpan>().TryParse(text, out _));
        Assert.False(SettingCodecs.For<DateTimeOffset>().TryParse(text, out _));
        Assert.False(SettingCodecs.For<Sample>().TryParse(text, out _));
        Assert.False(SettingCodecs.For<int>().TryParse(null, out _));
    }



    [Fact]
    public void Enums_parse_case_insensitively_by_name_only_and_list_their_choices()
    {
        var codec = SettingCodecs.For<Sample>();
        var explicitCodec = SettingCodecs.Enum<Sample>();

        Assert.True(codec.TryParse("second", out var value));
        Assert.Equal(Sample.Second, value);
        Assert.False(codec.TryParse("1", out _));   // numbers are not names
        Assert.False(codec.TryParse("Third", out _));
        Assert.Equal(["First", "Second"], codec.Choices);
        Assert.Equal(SettingKind.Enum, explicitCodec.Kind);
        Assert.Equal(["First", "Second"], explicitCodec.Choices);
        Assert.False(explicitCodec.TryParse("7", out _));
        Assert.Equal("First", explicitCodec.Format(Sample.First));
    }



    [Fact]
    public void Doubles_must_be_finite_and_strings_never_null()
    {
        Assert.False(SettingCodecs.For<double>().TryParse("NaN", out _));
        Assert.Equal(string.Empty, SettingCodecs.For<string>().Format(null!));
        Assert.Null(SettingCodecs.For<string>().Choices);
    }



    [Fact]
    public void Json_codec_uses_the_supplied_serializer_and_treats_failures_as_invalid()
    {
        var codec = SettingCodecs.Json<int[]>(v => string.Join(',', v), text => text.Length == 0 ? null : text.Split(',').Select(int.Parse).ToArray());

        Assert.Equal(SettingKind.Json, codec.Kind);
        Assert.Equal("1,2", codec.Format([1, 2]));
        Assert.True(codec.TryParse("3,4", out var value));
        Assert.Equal([3, 4], value);
        Assert.False(codec.TryParse("x", out _));   // FormatException → invalid
        Assert.False(codec.TryParse("", out _));    // null → invalid
        Assert.Throws<ArgumentNullException>(() => SettingCodecs.Json<int>(null!, _ => 0));
        Assert.Throws<ArgumentNullException>(() => SettingCodecs.Json<int>(_ => "", null!));
    }



    [Fact]
    public void Unsupported_types_have_no_built_in_codec()
    {
        Assert.Throws<NotSupportedException>(() => SettingCodecs.For<int[]>());
        Assert.Throws<NotSupportedException>(() => SettingCodecs.For<Uri>());
    }



    [Fact]
    public void Codecs_are_shared_per_type_and_require_their_delegates()
    {
        Assert.Same(SettingCodecs.For<int>(), SettingCodecs.For<int>());
        Assert.Throws<ArgumentNullException>(() => new SettingCodec<int>(SettingKind.Integer, null!, (string t, out int v) => int.TryParse(t, out v)));
        Assert.Throws<ArgumentNullException>(() => new SettingCodec<int>(SettingKind.Integer, _ => "", null!));
    }



    [Fact]
    public void Secret_text_masks_itself()
    {
        Assert.Equal("••••••", new SecretText("hunter2").ToString());
        Assert.Equal(string.Empty, new SecretText(string.Empty).ToString());
        Assert.True(new SecretText(string.Empty).IsEmpty);
        Assert.False(new SecretText("x").IsEmpty);
        Assert.Throws<ArgumentNullException>(() => new SecretText(null!));
    }



    private static void AssertRoundTrip<T>(T value, string expectedText, SettingKind expectedKind)
    {
        var codec = SettingCodecs.For<T>();

        Assert.Equal(expectedKind, codec.Kind);
        Assert.Equal(expectedText, codec.Format(value));
        Assert.True(codec.TryParse(expectedText, out var parsed), $"{typeof(T).Name}: '{expectedText}' must parse");
        Assert.Equal(value, parsed);
    }
}
