// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.AotSmoke;

/// <summary>
/// <see cref="SettingCodecs"/> picks a codec per value type through a generic static cache that branches on
/// <c>typeof(T)</c>, and handles enums with the non-generic <c>Enum.GetNames(Type)</c>, <c>Enum.TryParse(Type, …)</c>
/// and <c>Enum.IsDefined(Type, …)</c>. Under NativeAOT every generic instantiation and every enum's metadata must be
/// present at run time, so each built-in type is round-tripped here.
/// </summary>
internal static class SettingCodecsSmoke
{
    public static void EveryBuiltInTypeRoundTrips()
    {
        RoundTrip(true, "true", SettingKind.Boolean);
        RoundTrip(42, "42", SettingKind.Integer);
        RoundTrip(9_000_000_000L, "9000000000", SettingKind.Integer);
        RoundTrip(12.5m, "12.5", SettingKind.Number);
        RoundTrip(0.25d, "0.25", SettingKind.Number);
        RoundTrip("tote", "tote", SettingKind.String);
        RoundTrip(TimeSpan.FromMinutes(15), "00:15:00", SettingKind.Duration);
        RoundTrip(new DateTimeOffset(2026, 9, 27, 8, 30, 0, TimeSpan.Zero), "2026-09-27T08:30:00.0000000+00:00", SettingKind.Timestamp);
        RoundTrip(new SecretText("s3cret"), "s3cret", SettingKind.Secret);
    }



    public static void EnumThroughTheBuiltInCodec()
    {
        var codec = SettingCodecs.For<Shade>();

        Smoke.SequenceEqual([nameof(SettingKind.Enum)], [codec.Kind.ToString()]);
        Smoke.SequenceEqual(["Red", "Green", "Blue"], codec.Choices ?? []);
        Smoke.SequenceEqual(["Green"], [codec.Format(Shade.Green)]);
        Smoke.SequenceEqual(["Blue"], [Parse(codec, "blue").ToString()]);
        Rejects(codec, "1");
        Rejects(codec, "Purple");
    }



    public static void EnumThroughTheTypedCodec()
    {
        var codec = SettingCodecs.Enum<Shade>();

        Smoke.SequenceEqual(["Red", "Green", "Blue"], codec.Choices ?? []);
        Smoke.SequenceEqual(["Red"], [Parse(codec, "RED").ToString()]);
        Rejects(codec, "2");
    }



    public static void JsonCodecAndUnsupportedType()
    {
        var codec = SettingCodecs.Json<string>(value => "\"" + value + "\"", text => text.Length > 1 ? text[1..^1] : null);

        Smoke.SequenceEqual(["\"wave\""], [codec.Format("wave")]);
        Smoke.SequenceEqual(["wave"], [Parse(codec, "\"wave\"")]);
        Smoke.Throws<NotSupportedException>(() => SettingCodecs.For<Guid>(), nameof(Guid));
    }



    private static void RoundTrip<T>(T value, string text, SettingKind kind)
    {
        var codec = SettingCodecs.For<T>();

        Smoke.SequenceEqual([kind.ToString()], [codec.Kind.ToString()]);
        Smoke.SequenceEqual([text], [codec.Format(value)]);
        var parsed = Parse(codec, text);
        if (!EqualityComparer<T>.Default.Equals(parsed, value))
        {
            throw new SmokeFailureException($"{typeof(T).Name}: '{text}' parsed to '{parsed}', expected '{value}'");
        }
    }



    private static T Parse<T>(SettingCodec<T> codec, string text)
    {
        return codec.TryParse(text, out var value)
            ? value
            : throw new SmokeFailureException($"{typeof(T).Name}: '{text}' did not parse");
    }



    private static void Rejects<T>(SettingCodec<T> codec, string text)
    {
        if (codec.TryParse(text, out _))
        {
            throw new SmokeFailureException($"{typeof(T).Name}: '{text}' parsed but should be rejected");
        }
    }



    private enum Shade
    {
        Red,
        Green,
        Blue,
    }
}
