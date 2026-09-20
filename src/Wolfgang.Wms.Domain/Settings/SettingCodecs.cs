// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Wolfgang.Wms.Domain.Settings;

/// <summary>
/// The built-in codecs (E6.1): one per <see cref="SettingKind"/>, invariant culture throughout, shared per
/// value type so two keys of the same type compare equal.
/// </summary>
public static class SettingCodecs
{
    /// <summary>
    /// The codec for a built-in value type: <c>bool</c>, <c>int</c>, <c>long</c>, <c>decimal</c>, <c>double</c>,
    /// <c>string</c>, <see cref="TimeSpan"/>, <see cref="DateTimeOffset"/>, <see cref="SecretText"/> or any
    /// enum.
    /// </summary>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> has no built-in codec; pass a JSON codec to the key.</exception>
    public static SettingCodec<T> For<T>()
    {
        return Cache<T>.Instance ?? throw new NotSupportedException($"No built-in setting codec for {typeof(T).Name}; give the key a JSON codec (SettingCodecs.Json).");
    }



    /// <summary>
    /// A codec for a structured value stored as JSON, with the serializer the key's author supplies (a
    /// source-generated <c>JsonTypeInfo</c> in modules, so the host stays trim-safe). A deserializer that
    /// throws or returns null makes the text invalid.
    /// </summary>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static SettingCodec<T> Json<T>(Func<T, string> serialize, Func<string, T?> deserialize)
    {
        ArgumentNullException.ThrowIfNull(serialize);
        ArgumentNullException.ThrowIfNull(deserialize);

        return new SettingCodec<T>(SettingKind.Json, serialize, (string text, [MaybeNullWhen(false)] out T value) =>
        {
            try
            {
                value = deserialize(text);
                return value is not null;
            }
            catch (Exception exception) when (exception is FormatException or InvalidOperationException or NotSupportedException or ArgumentException)
            {
                value = default;
                return false;
            }
        });
    }



    /// <summary>
    /// The codec for an enum: stored by name, parsed case-insensitively, names listed as the choices.
    /// </summary>
    public static SettingCodec<TEnum> Enum<TEnum>()
        where TEnum : struct, Enum
    {
        return new SettingCodec<TEnum>
        (
            SettingKind.Enum,
            value => value.ToString(),
            TryParseEnum,
            System.Enum.GetNames<TEnum>()
        );
    }



    private static SettingCodec<T>? Create<T>()
    {
        if (typeof(T).IsEnum)
        {
            return new SettingCodec<T>(SettingKind.Enum, value => value!.ToString()!, TryParseEnum, System.Enum.GetNames(typeof(T)));
        }

        if (typeof(T) == typeof(bool))
        {
            return As<T, bool>(new SettingCodec<bool>(SettingKind.Boolean, v => v ? "true" : "false", (string t, out bool v) => bool.TryParse(t, out v)));
        }

        if (typeof(T) == typeof(int))
        {
            return As<T, int>(new SettingCodec<int>(SettingKind.Integer, v => v.ToString(CultureInfo.InvariantCulture), (string t, out int v) => int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)));
        }

        if (typeof(T) == typeof(long))
        {
            return As<T, long>(new SettingCodec<long>(SettingKind.Integer, v => v.ToString(CultureInfo.InvariantCulture), (string t, out long v) => long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)));
        }

        if (typeof(T) == typeof(decimal))
        {
            return As<T, decimal>(new SettingCodec<decimal>(SettingKind.Number, v => v.ToString(CultureInfo.InvariantCulture), (string t, out decimal v) => decimal.TryParse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out v)));
        }

        if (typeof(T) == typeof(double))
        {
            return As<T, double>(new SettingCodec<double>(SettingKind.Number, v => v.ToString("R", CultureInfo.InvariantCulture), (string t, out double v) => double.TryParse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out v) && double.IsFinite(v)));
        }

        return CreateTextual<T>();
    }



    private static SettingCodec<T>? CreateTextual<T>()
    {
        if (typeof(T) == typeof(string))
        {
            return As<T, string>(new SettingCodec<string>(SettingKind.String, v => v ?? string.Empty, (string t, [MaybeNullWhen(false)] out string v) => { v = t; return true; }));
        }

        if (typeof(T) == typeof(TimeSpan))
        {
            return As<T, TimeSpan>(new SettingCodec<TimeSpan>(SettingKind.Duration, v => v.ToString("c", CultureInfo.InvariantCulture), (string t, out TimeSpan v) => TimeSpan.TryParseExact(t, "c", CultureInfo.InvariantCulture, out v)));
        }

        if (typeof(T) == typeof(DateTimeOffset))
        {
            return As<T, DateTimeOffset>(new SettingCodec<DateTimeOffset>(SettingKind.Timestamp, v => v.ToString("O", CultureInfo.InvariantCulture), (string t, out DateTimeOffset v) => DateTimeOffset.TryParseExact(t, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out v)));
        }

        if (typeof(T) == typeof(SecretText))
        {
            return As<T, SecretText>(new SettingCodec<SecretText>(SettingKind.Secret, v => v.Value, (string t, out SecretText v) => { v = new SecretText(t); return true; }));
        }

        return null;
    }



    private static bool TryParseEnum<T>(string text, [MaybeNullWhen(false)] out T value)
    {
        if (IsName(text) && System.Enum.TryParse(typeof(T), text, ignoreCase: true, out var parsed) && System.Enum.IsDefined(typeof(T), parsed!))
        {
            value = (T)parsed!;
            return true;
        }

        value = default;
        return false;
    }



    /// <summary>
    /// Enums are stored by name only; <c>Enum.TryParse</c> would also accept "1", which is not a choice.
    /// </summary>
    private static bool IsName(string text)
    {
        return text.Length > 0 && char.IsLetter(text[0]);
    }



    private static SettingCodec<T> As<T, TActual>(SettingCodec<TActual> codec)
    {
        return (SettingCodec<T>)(object)codec;
    }



    private static class Cache<T>
    {
        internal static readonly SettingCodec<T>? Instance = Create<T>();
    }
}
