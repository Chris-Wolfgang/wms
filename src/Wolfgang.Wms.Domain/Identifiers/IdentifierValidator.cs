// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;

namespace Wolfgang.Wms.Domain.Identifiers;

/// <summary>
/// Applies a <see cref="ValidationProfile"/> to a customer-supplied value (E3.6, E3.7), identically at intake,
/// scan, console, CSV import and API: normalise (trim, case), then reject control characters, then check
/// required, length and format. Pure and shared with the device (E1.4).
/// </summary>
public static class IdentifierValidator
{
    /// <summary>
    /// Validates <paramref name="raw"/> against <paramref name="profile"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    public static IdentifierValidation Validate(ValidationProfile profile, string? raw)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var value = Normalize(profile, raw);
        if (value.Length == 0)
        {
            return profile.Required
                ? IdentifierValidation.Failure(profile.Field, "required", "a value is required")
                : IdentifierValidation.Success(profile.Field, value);
        }

        if (value.Any(char.IsControl))
        {
            return IdentifierValidation.Failure(profile.Field, "control_characters", "no control characters");
        }

        if (value.Length < profile.MinLength)
        {
            return IdentifierValidation.Failure(profile.Field, "min_length", $"at least {profile.MinLength.ToString(CultureInfo.InvariantCulture)} characters");
        }

        if (value.Length > profile.EffectiveMaxLength)
        {
            return IdentifierValidation.Failure(profile.Field, "max_length", $"at most {profile.EffectiveMaxLength.ToString(CultureInfo.InvariantCulture)} characters");
        }

        if (profile.Format is not null && !profile.Format.IsMatch(value))
        {
            return IdentifierValidation.Failure(profile.Field, "format", $"format {profile.Format.Source}");
        }

        return IdentifierValidation.Success(profile.Field, value);
    }



    /// <summary>
    /// The normalised form of a value under a profile: trimmed as configured, case handled, never null.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    public static string Normalize(ValidationProfile profile, string? raw)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var value = raw ?? string.Empty;
        if (profile.TrimLeading)
        {
            value = value.TrimStart(' ');
        }

        if (profile.TrimTrailing)
        {
            value = value.TrimEnd(' ');
        }

        return profile.Case switch
        {
            CaseHandling.MakeUpper => value.ToUpperInvariant(),
            CaseHandling.MakeLower => value.ToLowerInvariant(),
            _ => value,
        };
    }
}
