// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// Validation shared by every key type: a key name is a stable, lower-case, dotted identifier such as
/// <c>picking.lease_timeout</c> or <c>devices.remote_logging</c>. Names are compared ordinally.
/// </summary>
public static class KeyName
{
    /// <summary>
    /// Returns <paramref name="name"/> when it is a valid key name; throws otherwise.
    /// </summary>
    /// <exception cref="ArgumentException">The name is empty or not a lower-case dotted identifier.</exception>
    public static string Require(string name, string paramName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A key name is required.", paramName);
        }

        if (!IsValid(name))
        {
            throw new ArgumentException($"'{name}' is not a valid key name: use lower-case letters, digits, '_' and '-' in dot-separated segments that start with a letter.", paramName);
        }

        return name;
    }



    /// <summary>
    /// True when <paramref name="name"/> is one or more dot-separated segments, each a letter followed by
    /// lower-case letters, digits, underscores or hyphens.
    /// </summary>
    public static bool IsValid(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (var segment in name.Split('.'))
        {
            if (segment.Length == 0 || !char.IsAsciiLetterLower(segment[0]))
            {
                return false;
            }

            foreach (var c in segment)
            {
                if (!(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '_' or '-'))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
