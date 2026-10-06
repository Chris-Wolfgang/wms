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
    /// The ISO 3166-1 numeric ("num-3") country codes, as listed by the GS1 Barcode Syntax Dictionary's
    /// <c>iso3166</c> linter; AI 421 must start with one.
    /// </summary>
    private static readonly HashSet<string> Iso3166Numeric = new
    (
        "004 008 010 012 016 020 024 028 031 032 036 040 044 048 050 051 052 056 060 064 068 070 072 074 076 084 086 090 092 096 100 104 108 112 116 120 124 132 136 140 144 148 152 156 158 162 166 170 174 175 178 180 184 188 191 192 196 203 204 208 212 214 218 222 226 231 232 233 234 238 239 242 246 248 250 254 258 260 262 266 268 270 275 276 288 292 296 300 304 308 312 316 320 324 328 332 334 336 340 344 348 352 356 360 364 368 372 376 380 384 388 392 398 400 404 408 410 414 417 418 422 426 428 430 434 438 440 442 446 450 454 458 462 466 470 474 478 480 484 492 496 498 499 500 504 508 512 516 520 524 528 531 533 534 535 540 548 554 558 562 566 570 574 578 580 581 583 584 585 586 591 598 600 604 608 612 616 620 624 626 630 634 638 642 643 646 652 654 659 660 662 663 666 670 674 678 682 686 688 690 694 702 703 704 705 706 710 716 724 728 729 732 740 744 748 752 756 760 762 764 768 772 776 780 784 788 792 795 796 798 800 804 807 818 826 831 832 833 834 840 850 854 858 860 862 876 882 887 894".Split(' '),
        StringComparer.Ordinal
    );

    /// <summary>
    /// The AIM symbology identifiers that announce GS1 data: GS1-128, GS1 DataBar, GS1 DataMatrix, GS1 QR Code,
    /// GS1 DotCode.
    /// </summary>
    private static readonly string[] Gs1SymbologyIdentifiers = ["]C1", "]e0", "]d2", "]Q3", "]J1"];



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
    /// True for a <c>YYMMDDhhmm</c> GS1 date and time (AI 7003): a real calendar date (day <c>00</c> is not
    /// allowed here), hour <c>00</c>–<c>23</c> and minute <c>00</c>–<c>59</c>.
    /// </summary>
    public static bool IsValidDateTime(string? value)
    {
        if (value is not { Length: 10 } || !value.All(char.IsAsciiDigit) || !IsValidDate(value[..6]) || string.Equals(value[4..6], "00", StringComparison.Ordinal))
        {
            return false;
        }

        var hour = int.Parse(value.AsSpan(6, 2), CultureInfo.InvariantCulture);
        var minute = int.Parse(value.AsSpan(8, 2), CultureInfo.InvariantCulture);
        return hour <= 23 && minute <= 59;
    }



    /// <summary>
    /// True when <paramref name="value"/> starts with an AIM symbology identifier that announces GS1 data
    /// (<c>]C1</c> GS1-128, <c>]e0</c> GS1 DataBar, <c>]d2</c> GS1 DataMatrix, <c>]Q3</c> GS1 QR Code, <c>]J1</c>
    /// GS1 DotCode): the scanner says the value is a GS1 element string.
    /// </summary>
    public static bool HasGs1SymbologyIdentifier(string? value)
    {
        return value is { Length: >= 3 } && Gs1SymbologyIdentifiers.Contains(value[..3], StringComparer.Ordinal);
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

        // Not trimmed: a value is kept exactly as encoded, and a stray tab or line break is a control character.
        var span = text.AsSpan();
        // Only the GS1 symbology identifiers are scanner metadata; any other ']..' prefix is parsed (and rejected).
        if (span.Length > 3 && HasGs1SymbologyIdentifier(text))
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
            { Rule: Gs1ValueRule.DateTime } when !IsValidDateTime(value) => $"({ai.Code}) {ai.Description}: not a YYMMDDhhmm date and time.",
            { Rule: Gs1ValueRule.CountryAndPostalCode } when !IsCountryAndPostalCode(value) => $"({ai.Code}) {ai.Description}: expected a 3-digit ISO 3166 country code and 1 to 9 more characters.",
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



    private static bool IsCountryAndPostalCode(string value)
    {
        return value.Length >= 4 && Iso3166Numeric.Contains(value[..3]);
    }



    private static int IndexOrEnd(ReadOnlySpan<char> span, char separator)
    {
        var index = span.IndexOf(separator);
        return index < 0 ? span.Length : index;
    }
}
