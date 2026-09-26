// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Wolfgang.Wms.Domain.Identifiers;

/// <summary>
/// Compiles the mask syntax of E3.8 to an anchored .NET regular expression: <c>A</c> → letter, <c>9</c> →
/// digit, <c>X</c> → letter or digit, <c>?</c> → any character, <c>(n)</c> → repeat the preceding token n
/// times, any other character → itself (escaped). Runs of the same token collapse to a count, so
/// <c>AAA-999</c> → <c>^[A-Za-z]{3}-[0-9]{3}$</c>, the form the settings page shows.
/// </summary>
public static class MaskCompiler
{
    /// <summary>
    /// The regular expression for a mask.
    /// </summary>
    /// <exception cref="ArgumentException">The mask is blank, has a repeat with nothing before it, an unclosed
    /// or non-numeric repeat, or a repeat count of zero.</exception>
    public static string ToRegex(string mask)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mask);

        var pattern = new StringBuilder("^");
        string? run = null;
        var runLength = 0;
        for (var i = 0; i < mask.Length; i++)
        {
            var c = mask[i];
            if (c == '(')
            {
                var close = mask.IndexOf(')', i + 1);
                if (close < 0 || run is null || !int.TryParse(mask.AsSpan(i + 1, close - i - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
                {
                    throw new ArgumentException($"Mask '{mask}': '(n)' must follow a token and n must be a positive number.", nameof(mask));
                }

                Flush(pattern, run, runLength - 1);
                Flush(pattern, run, count);
                run = null;
                runLength = 0;
                i = close;
                continue;
            }

            var token = c switch
            {
                'A' => "[A-Za-z]",
                '9' => "[0-9]",
                'X' => "[A-Za-z0-9]",
                '?' => ".",
                _ => Regex.Escape(c.ToString()),
            };

            if (string.Equals(token, run, StringComparison.Ordinal))
            {
                runLength++;
                continue;
            }

            Flush(pattern, run, runLength);
            run = token;
            runLength = 1;
        }

        Flush(pattern, run, runLength);
        return pattern.Append('$').ToString();
    }



    private static void Flush(StringBuilder pattern, string? token, int count)
    {
        if (token is null || count <= 0)
        {
            return;
        }

        pattern.Append(token);
        if (count > 1)
        {
            pattern.Append(CultureInfo.InvariantCulture, $"{{{count}}}");
        }
    }
}
