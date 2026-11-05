// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Locations;

/// <summary>
/// What a location draft must satisfy (E17.1), checked the same way by the store, the import and the console.
/// </summary>
public static class LocationRules
{
    /// <summary>Longest bin code.</summary>
    public const int CodeLength = 64;

    /// <summary>Longest barcode.</summary>
    public const int BarcodeLength = 128;

    /// <summary>Longest walk sequence.</summary>
    public const int WalkSequenceLength = 64;



    /// <summary>
    /// Null when <paramref name="draft"/> is acceptable on its own, else the first reason it is not. The store
    /// adds what needs the database: the zone belongs to the site, the walk sequence starts with the zone's
    /// prefix, the code and the barcode are free.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public static string? Validate(LocationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return Code(draft.Code)
            ?? Barcode(draft.Barcode)
            ?? (draft.ZoneId > 0 ? null : "zoneId is required.")
            ?? WalkSequence(draft.WalkSequence);
    }



    /// <summary>
    /// The form codes are compared in: trimmed, upper-case invariant.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public static string Normalize(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return code.Trim().ToUpperInvariant();
    }



    /// <summary>
    /// Null when <paramref name="walkSequence"/> starts with the zone's walk-order prefix (or the zone has none),
    /// else the reason.
    /// </summary>
    public static string? WalkSequenceUnderPrefix(string walkSequence, string? zonePrefix)
    {
        if (string.IsNullOrEmpty(zonePrefix) || (walkSequence ?? string.Empty).Trim().StartsWith(zonePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"walkSequence must start with the zone's walk-order prefix '{zonePrefix}'.";
    }



    private static string? Code(string? code)
    {
        if (Text("code", code, CodeLength) is { } reason)
        {
            return reason;
        }

        return code!.Trim().All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? null : "code may contain letters, digits, '-' and '_' only.";
    }



    private static string? Barcode(string? barcode)
    {
        if (Text("barcode", barcode, BarcodeLength) is { } reason)
        {
            return reason;
        }

        return barcode!.Trim().All(c => c is > ' ' and <= '~' && c != '|') ? null : "barcode may contain visible ASCII characters only, without spaces or '|'.";
    }



    private static string? WalkSequence(string? walkSequence)
    {
        if (Text("walkSequence", walkSequence, WalkSequenceLength) is { } reason)
        {
            return reason;
        }

        return walkSequence!.Trim().All(c => c is > ' ' and <= '~' && c != '|') ? null : "walkSequence may contain visible ASCII characters only, without spaces or '|'.";
    }



    private static string? Text(string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{field} is required.";
        }

        return value.Trim().Length <= maxLength ? null : $"{field} must be at most {maxLength} characters.";
    }
}
