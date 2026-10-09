// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text;

namespace Wolfgang.Wms.Infrastructure.Database.Conventions;

/// <summary>
/// The one naming transform for database identifiers (E3.2): <c>ZoneGroupId</c> → <c>zone_group_id</c>,
/// <c>ErpReleaseId</c> → <c>erp_release_id</c>, <c>SKUCode</c> → <c>sku_code</c>. Lower-case ASCII letters,
/// digits and single underscores only, so hand-written SQL needs no quoting on either engine.
/// </summary>
public static class SnakeCase
{
    /// <summary>
    /// The snake_case form of a CLR identifier.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public static string Of(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var result = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c == '_' || c == ' ')
            {
                if (result.Length > 0 && result[^1] != '_')
                {
                    result.Append('_');
                }

                continue;
            }

            if (char.IsUpper(c) && result.Length > 0 && result[^1] != '_')
            {
                var previous = name[i - 1];
                var startsWord = char.IsLower(previous) || char.IsDigit(previous)
                    || (char.IsUpper(previous) && i + 1 < name.Length && char.IsLower(name[i + 1]));
                if (startsWord)
                {
                    result.Append('_');
                }
            }

            result.Append(char.ToLowerInvariant(c));
        }

        return result.ToString().TrimEnd('_');
    }



    /// <summary>
    /// True when <paramref name="identifier"/> is already snake_case: lower-case letters, digits and single
    /// underscores, starting with a letter.
    /// </summary>
    public static bool Is(string? identifier)
    {
        if (string.IsNullOrEmpty(identifier) || !char.IsAsciiLetterLower(identifier[0]))
        {
            return false;
        }

        for (var i = 1; i < identifier.Length; i++)
        {
            var c = identifier[i];
            var ok = char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || (c == '_' && identifier[i - 1] != '_');
            if (!ok)
            {
                return false;
            }
        }

        return identifier[^1] != '_';
    }
}
