// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Copies;

/// <summary>
/// What a copy request must satisfy and how a code-prefix substitution is applied (E16.5).
/// </summary>
public static class CopyRules
{
    /// <summary>Longest prefix.</summary>
    public const int PrefixLength = 32;



    /// <summary>
    /// Null when a substitution pair is acceptable (both absent, or both present, non-blank and short enough),
    /// else the reason.
    /// </summary>
    public static string? ValidateSubstitution(string fieldFrom, string? from, string fieldTo, string? to)
    {
        if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return $"{fieldFrom} and {fieldTo} go together; give both or neither.";
        }

        if (from.Trim().Length > PrefixLength || to.Trim().Length > PrefixLength)
        {
            return $"{fieldFrom} and {fieldTo} must be at most {PrefixLength} characters.";
        }

        return from.Trim().Any(c => c is '|' or ' ') || to.Trim().Any(c => c is '|' or ' ') ? $"{fieldFrom} and {fieldTo} may not contain spaces or '|'." : null;
    }



    /// <summary>
    /// <paramref name="value"/> with <paramref name="from"/> replaced by <paramref name="to"/> at the start
    /// (compared without regard to case); unchanged when it does not start with <paramref name="from"/> or when
    /// there is no substitution.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static string Substitute(string value, string? from, string? to)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(from) || to is null)
        {
            return value;
        }

        var prefix = from.Trim();
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? to.Trim() + value[prefix.Length..] : value;
    }



    /// <summary>
    /// True when <paramref name="value"/> starts with <paramref name="prefix"/> (compared without regard to case).
    /// </summary>
    public static bool StartsWith(string? value, string? prefix)
    {
        return !string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(prefix) && value.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
