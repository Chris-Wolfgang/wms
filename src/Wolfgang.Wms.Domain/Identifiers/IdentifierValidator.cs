// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;

namespace Wolfgang.Wms.Domain.Identifiers;

/// <summary>
/// Applies a <see cref="ValidationProfile"/> to a customer-supplied value (E3.6, E3.7), identically at intake,
/// scan, console, CSV import and API: normalise (trim, case), then check required, then GS1 structure for a GS1
/// value (<see cref="ValidationProfile.Gs1ElementString"/> or a GS1 symbology identifier), otherwise reject
/// control characters, then check length and format. Pure and shared with the device (E1.4).
/// </summary>
public static class IdentifierValidator
{
    /// <summary>
    /// Validates <paramref name="raw"/> against <paramref name="profile"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="profile"/> has a
    /// <see cref="ValidationProfile.ConfigurationError"/>.</exception>
    public static IdentifierValidation Validate(ValidationProfile profile, string? raw)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.ConfigurationError is { } configurationError)
        {
            throw new ArgumentException(configurationError, nameof(profile));
        }

        // A GS1 element string is kept exactly as encoded: trimming or case handling would change its values.
        var gs1 = profile.Gs1ElementString || Gs1.HasGs1SymbologyIdentifier(raw);
        var value = gs1 ? raw ?? string.Empty : Normalize(profile, raw);
        if (value.Length == 0)
        {
            return profile.Required
                ? IdentifierValidation.Failure(profile.Field, "required", "a value is required")
                : IdentifierValidation.Success(profile.Field, value);
        }

        if (gs1)
        {
            // The parser rejects control characters inside values; FNC1 group separators between them are structure.
            if (!Gs1.TryParse(value, out _, out var gs1Error))
            {
                return IdentifierValidation.Failure(profile.Field, "gs1", gs1Error!);
            }
        }
        else if (value.Any(char.IsControl))
        {
            return IdentifierValidation.Failure(profile.Field, "control_characters", "control characters are not allowed");
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
