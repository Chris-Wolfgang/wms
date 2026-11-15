// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;

namespace Wolfgang.Wms.Core.Organization;

/// <summary>
/// What an organisation draft must satisfy (E16.0), checked the same way by the store and by the console.
/// </summary>
public static class OrganizationRules
{
    /// <summary>Longest name.</summary>
    public const int NameLength = 128;

    /// <summary>Longest legal name.</summary>
    public const int LegalNameLength = 256;

    /// <summary>Longest logo data URL (256 KB of text: a small PNG or SVG).</summary>
    public const int LogoLength = 262_144;

    /// <summary>Longest time zone or culture id.</summary>
    public const int IdLength = 64;

    /// <summary>Longest address line, city, region, country or contact name.</summary>
    public const int TextLength = 256;

    /// <summary>Longest postal code.</summary>
    public const int PostalCodeLength = 32;

    /// <summary>Longest e-mail address or phone number.</summary>
    public const int ContactLength = 256;



    /// <summary>
    /// Null when <paramref name="draft"/> is acceptable, else the first reason it is not.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public static string? Validate(OrganizationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return Text("name", draft.Name, required: true, NameLength)
            ?? Text("legalName", draft.LegalName, required: false, LegalNameLength)
            ?? Logo(draft.LogoDataUrl)
            ?? TimeZone(draft.TimeZone)
            ?? Locale(draft.Locale)
            ?? Address(draft.Address)
            ?? Contact("primaryContact", draft.PrimaryContact)
            ?? Contact("supportContact", draft.SupportContact);
    }



    private static string? Logo(string? logo)
    {
        if (logo is null)
        {
            return null;
        }

        if (!logo.StartsWith("data:image/", StringComparison.Ordinal))
        {
            return "logoDataUrl must be a data:image/… URL.";
        }

        return logo.Length <= LogoLength ? null : $"logoDataUrl must be at most {LogoLength} characters.";
    }



    private static string? TimeZone(string? id)
    {
        if (Text("timeZone", id, required: true, IdLength) is { } reason)
        {
            return reason;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(id!.Trim(), out _) ? null : $"timeZone '{id}' is not a known time zone id.";
    }



    private static string? Locale(string? name)
    {
        if (Text("locale", name, required: true, IdLength) is { } reason)
        {
            return reason;
        }

        try
        {
            return CultureInfo.GetCultureInfo(name!.Trim(), predefinedOnly: true).Name.Length > 0 ? null : $"locale '{name}' is not a culture name.";
        }
        catch (CultureNotFoundException)
        {
            return $"locale '{name}' is not a culture name.";
        }
    }



    private static string? Address(OrganizationAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        return Text("address.line1", address.Line1, required: true, TextLength)
            ?? Text("address.line2", address.Line2, required: false, TextLength)
            ?? Text("address.city", address.City, required: true, TextLength)
            ?? Text("address.region", address.Region, required: false, TextLength)
            ?? Text("address.postalCode", address.PostalCode, required: true, PostalCodeLength)
            ?? Text("address.country", address.Country, required: true, TextLength);
    }



    private static string? Contact(string field, OrganizationContact? contact)
    {
        if (contact is null)
        {
            return null;
        }

        var reason = Text(field + ".name", contact.Name, required: true, TextLength)
            ?? Text(field + ".email", contact.Email, required: true, ContactLength)
            ?? Text(field + ".phone", contact.Phone, required: false, ContactLength);
        if (reason is not null)
        {
            return reason;
        }

        var at = contact.Email.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at < contact.Email.Length - 1 && !contact.Email.Contains(' ', StringComparison.Ordinal) ? null : $"{field}.email must be an e-mail address.";
    }



    private static string? Text(string field, string? value, bool required, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return required ? $"{field} is required." : null;
        }

        return value.Trim().Length <= maxLength ? null : $"{field} must be at most {maxLength} characters.";
    }
}
