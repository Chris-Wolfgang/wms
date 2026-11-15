// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Sites;

/// <summary>
/// What a site draft must satisfy (E16.1), checked the same way by the store and by the console.
/// </summary>
public static class SiteRules
{
    /// <summary>Longest code.</summary>
    public const int CodeLength = 32;

    /// <summary>Longest name.</summary>
    public const int NameLength = 128;

    /// <summary>Longest time zone id.</summary>
    public const int IdLength = 64;



    /// <summary>
    /// Null when <paramref name="draft"/> is acceptable, else the first reason it is not.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public static string? Validate(SiteDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return Code(draft.Code)
            ?? Text("name", draft.Name, NameLength)
            ?? TimeZone(draft.TimeZone);
    }



    /// <summary>
    /// The form codes are compared in: trimmed, upper-case invariant. <c>dc1</c> and <c>DC1</c> are the same site.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public static string Normalize(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return code.Trim().ToUpperInvariant();
    }



    private static string? Code(string? code)
    {
        if (Text("code", code, CodeLength) is { } reason)
        {
            return reason;
        }

        return code!.Trim().All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? null : "code may contain letters, digits, '-' and '_' only.";
    }



    private static string? TimeZone(string? id)
    {
        if (Text("timeZone", id, IdLength) is { } reason)
        {
            return reason;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(id!.Trim(), out _) ? null : $"timeZone '{id}' is not a known time zone id.";
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
