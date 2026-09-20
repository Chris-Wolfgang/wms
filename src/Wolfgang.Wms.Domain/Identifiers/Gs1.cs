// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;

namespace Wolfgang.Wms.Domain.Identifiers;

/// <summary>
/// Structural GS1 validation (E3.6), applied by default and always: known application identifiers, fixed
/// and maximum lengths, digit-only values, GTIN/SSCC/GLN check digits and <c>YYMMDD</c> dates. Per-SKU lot and
/// serial formats (E3.7) layer on top of what this accepts. Pure and shared with the device.
/// </summary>
public static class Gs1
{
    /// <summary>
    /// The group separator (FNC1) that ends a variable-length value in a scanned element string.
    /// </summary>
    public const char GroupSeparator = '';



    /// <summary>
    /// True for an 8-, 12-, 13- or 14-digit GTIN with a valid check digit.
    /// </summary>
    public static bool IsValidGtin(string? value)
    {
        return value is { Length: 8 or 12 or 13 or 14 } && HasValidCheckDigit(value);
    }



    /// <summary>
    /// True for an 18-digit SSCC with a valid check digit.
    /// </summary>
    public static bool IsValidSscc(string? value)
    {
        return value is { Length: 18 } && HasValidCheckDigit(value);
    }



    /// <summary>
    /// True when <paramref name="digits"/> is all digits and its last digit is the GS1 mod-10 check digit of
    /// the preceding ones (weights 3,1,3,… from the right).
    /// </summary>
    public static bool HasValidCheckDigit(string? digits)
    {
        if (string.IsNullOrEmpty(digits) || digits.Length < 2 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        return CheckDigit(digits.AsSpan(0, digits.Length - 1)) == digits[^1] - '0';
    }



    /// <summary>
    /// The GS1 mod-10 check digit for a run of digits.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="digits"/> is empty or not all digits.</exception>
    public static int CheckDigit(ReadOnlySpan<char> digits)
    {
        if (digits.IsEmpty)
        {
            throw new ArgumentException("At least one digit is required.", nameof(digits));
        }

        var sum = 0;
        var weight = 3;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            if (!char.IsAsciiDigit(digits[i]))
            {
                throw new ArgumentException("Digits only.", nameof(digits));
            }

            sum += (digits[i] - '0') * weight;
            weight = weight == 3 ? 1 : 3;
        }

        return (10 - (sum % 10)) % 10;
    }



    /// <summary>
    /// True for a <c>YYMMDD</c> GS1 date; day <c>00</c> means the last day of the month.
    /// </summary>
    public static bool IsValidDate(string? value)
    {
        if (value is not { Length: 6 } || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        var year = 2000 + int.Parse(value.AsSpan(0, 2), CultureInfo.InvariantCulture);
        var month = int.Parse(value.AsSpan(2, 2), CultureInfo.InvariantCulture);
        var day = int.Parse(value.AsSpan(4, 2), CultureInfo.InvariantCulture);
        return month is >= 1 and <= 12 && day >= 0 && day <= DateTime.DaysInMonth(year, month);
    }



    /// <summary>
    /// Parses and validates an element string, in the human-readable form <c>(01)09501101530003(17)261231(10)ABC</c>
    /// or the scanned form where fixed-length values run together and variable-length ones end with
    /// <see cref="GroupSeparator"/> (a leading symbology identifier such as <c>]C1</c> or <c>]d2</c> is ignored).
    /// </summary>
    /// <param name="text">The element string.</param>
    /// <param name="elements">The parsed elements in order, empty on failure.</param>
    /// <param name="error">Why the string is invalid, naming the AI where possible; null on success.</param>
    public static bool TryParse(string? text, out IReadOnlyList<Gs1Element> elements, out string? error)
    {
        var list = new List<Gs1Element>();
        elements = list;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "An element string is required.";
            return false;
        }

        var span = text.AsSpan().Trim();
        if (span.Length > 3 && span[0] == ']')
        {
            span = span[3..];
        }

        var ok = span.Length > 0 && span[0] == '(' ? ParseBracketed(span, list, out error) : ParseScanned(span, list, out error);
        if (ok && list.Count == 0)
        {
            error = "No application identifiers found.";
            ok = false;
        }

        if (!ok)
        {
            list.Clear();
        }

        return ok;
    }



    private static bool ParseBracketed(ReadOnlySpan<char> span, List<Gs1Element> list, out string? error)
    {
        error = null;
        while (!span.IsEmpty)
        {
            var close = span.IndexOf(')');
            if (span[0] != '(' || close < 0)
            {
                error = "Expected '(AI)' at '" + span.ToString() + "'.";
                return false;
            }

            var code = span[1..close].ToString();
            var ai = Gs1ApplicationIdentifier.Find(code);
            if (ai is null || !string.Equals(ai.Code, code, StringComparison.Ordinal))
            {
                error = "Unknown application identifier '" + code + "'.";
                return false;
            }

            span = span[(close + 1)..];
            var end = ai.IsFixedLength ? Math.Min(ai.FixedLength!.Value, span.Length) : IndexOrEnd(span, '(');
            if (!TryTake(ai, span[..end], list, out error))
            {
                return false;
            }

            span = span[end..];
        }

        return true;
    }



    private static bool ParseScanned(ReadOnlySpan<char> span, List<Gs1Element> list, out string? error)
    {
        error = null;
        while (!span.IsEmpty)
        {
            if (span[0] == GroupSeparator)
            {
                span = span[1..];
                continue;
            }

            var ai = Gs1ApplicationIdentifier.Find(span);
            if (ai is null)
            {
                error = "Unknown application identifier at '" + span[..Math.Min(4, span.Length)].ToString() + "'.";
                return false;
            }

            span = span[ai.Code.Length..];
            var end = ai.IsFixedLength ? Math.Min(ai.FixedLength!.Value, span.Length) : IndexOrEnd(span, GroupSeparator);
            if (!TryTake(ai, span[..end], list, out error))
            {
                return false;
            }

            span = span[end..];
        }

        return true;
    }



    private static bool TryTake(Gs1ApplicationIdentifier ai, ReadOnlySpan<char> raw, List<Gs1Element> list, out string? error)
    {
        var value = raw.ToString();
        error = ai switch
        {
            { IsFixedLength: true } when value.Length != ai.FixedLength => $"({ai.Code}) {ai.Description}: expected {ai.FixedLength} characters, got {value.Length}.",
            { IsFixedLength: false } when value.Length == 0 || value.Length > ai.MaxLength => $"({ai.Code}) {ai.Description}: expected 1 to {ai.MaxLength} characters, got {value.Length}.",
            { Numeric: true } when !value.All(char.IsAsciiDigit) => $"({ai.Code}) {ai.Description}: digits only.",
            { Rule: Gs1ValueRule.CheckDigit } when !HasValidCheckDigit(value) => $"({ai.Code}) {ai.Description}: check digit is wrong.",
            { Rule: Gs1ValueRule.Date } when !IsValidDate(value) => $"({ai.Code}) {ai.Description}: not a YYMMDD date.",
            _ when value.Any(char.IsControl) => $"({ai.Code}) {ai.Description}: control characters are not allowed.",
            _ => null,
        };

        if (error is not null)
        {
            return false;
        }

        list.Add(new Gs1Element(ai, value));
        return true;
    }



    private static int IndexOrEnd(ReadOnlySpan<char> span, char separator)
    {
        var index = span.IndexOf(separator);
        return index < 0 ? span.Length : index;
    }
}
