// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Wms.Domain.Settings;

/// <summary>
/// Converts a setting value to and from its stored text (E6.1, E6.3): invariant culture, one format per
/// kind, so a value written by the console, the API or the CLI reads back identically on every engine.
/// <see cref="SettingCodecs.For{T}"/> supplies the codec for the built-in kinds; a JSON-valued key passes
/// its own (<see cref="SettingCodecs.Json{T}"/>).
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class SettingCodec<T>
{
    private readonly Func<T, string> _format;
    private readonly TryParseSetting<T> _tryParse;



    /// <summary>
    /// Creates a codec.
    /// </summary>
    /// <param name="kind">The kind the codec stores.</param>
    /// <param name="format">Value → stored text.</param>
    /// <param name="tryParse">Stored text → value.</param>
    /// <param name="choices">The allowed texts when the kind is a fixed list, else null.</param>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public SettingCodec(SettingKind kind, Func<T, string> format, TryParseSetting<T> tryParse, IReadOnlyList<string>? choices = null)
    {
        Kind = kind;
        _format = format ?? throw new ArgumentNullException(nameof(format));
        _tryParse = tryParse ?? throw new ArgumentNullException(nameof(tryParse));
        Choices = choices;
    }



    /// <summary>
    /// The kind the codec stores.
    /// </summary>
    public SettingKind Kind { get; }



    /// <summary>
    /// The allowed texts when the kind is a fixed list (enum names), else null.
    /// </summary>
    public IReadOnlyList<string>? Choices { get; }



    /// <summary>
    /// The stored text of a value.
    /// </summary>
    public string Format(T value)
    {
        return _format(value);
    }



    /// <summary>
    /// Parses stored text; false when the text is not a valid value of this kind.
    /// </summary>
    public bool TryParse(string? text, [MaybeNullWhen(false)] out T value)
    {
        if (text is null)
        {
            value = default;
            return false;
        }

        return _tryParse(text, out value);
    }
}
