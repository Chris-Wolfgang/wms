// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Organization;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Organization;

/// <summary>
/// The one row of <c>core.organization</c> (E16.0): the company the install belongs to. Audited (E6.4) and
/// versioned (E5.1); the address and the two contacts are flattened into columns because they are read and
/// written only with the row.
/// </summary>
public sealed class Organization : IVersionedEntity
{
    /// <summary>The server-assigned identifier; the row is a singleton.</summary>
    public long Id { get; set; }

    /// <summary>The name users see.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The registered company name; null to use <see cref="Name"/>.</summary>
    public string? LegalName { get; set; }

    /// <summary>The logo as a <c>data:image/…</c> URL; null for none.</summary>
    public string? LogoDataUrl { get; set; }

    /// <summary>The default time zone id.</summary>
    public string TimeZone { get; set; } = string.Empty;

    /// <summary>The default culture name.</summary>
    public string Locale { get; set; } = string.Empty;

    /// <summary>Street and number; null when no address is known.</summary>
    public string? AddressLine1 { get; set; }

    /// <summary>Second address line.</summary>
    public string? AddressLine2 { get; set; }

    /// <summary>City or town.</summary>
    public string? AddressCity { get; set; }

    /// <summary>State, province or county.</summary>
    public string? AddressRegion { get; set; }

    /// <summary>Postal or ZIP code.</summary>
    public string? AddressPostalCode { get; set; }

    /// <summary>Country.</summary>
    public string? AddressCountry { get; set; }

    /// <summary>The primary contact's name; null when none is known.</summary>
    public string? PrimaryContactName { get; set; }

    /// <summary>The primary contact's e-mail address.</summary>
    public string? PrimaryContactEmail { get; set; }

    /// <summary>The primary contact's phone number.</summary>
    public string? PrimaryContactPhone { get; set; }

    /// <summary>The support contact's name; null to use the primary contact.</summary>
    public string? SupportContactName { get; set; }

    /// <summary>The support contact's e-mail address.</summary>
    public string? SupportContactEmail { get; set; }

    /// <summary>The support contact's phone number.</summary>
    public string? SupportContactPhone { get; set; }

    /// <summary>When the row was last written (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Who last wrote the row.</summary>
    public string UpdatedBy { get; set; } = string.Empty;

    /// <inheritdoc/>
    public long RowVersion { get; set; }



    /// <summary>
    /// The row as the API reports it.
    /// </summary>
    public OrganizationInfo ToInfo()
    {
        var address = AddressLine1 is null ? null : new OrganizationAddress(AddressLine1, AddressLine2, AddressCity ?? string.Empty, AddressRegion, AddressPostalCode ?? string.Empty, AddressCountry ?? string.Empty);
        var primary = PrimaryContactName is null ? null : new OrganizationContact(PrimaryContactName, PrimaryContactEmail ?? string.Empty, PrimaryContactPhone);
        var support = SupportContactName is null ? null : new OrganizationContact(SupportContactName, SupportContactEmail ?? string.Empty, SupportContactPhone);
        return new OrganizationInfo(Id, Name, LegalName, LogoDataUrl, TimeZone, Locale, address, primary, support, UpdatedAt, UpdatedBy, RowVersion);
    }



    /// <summary>
    /// Copies a valid draft into the row.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public void Apply(OrganizationDraft draft, DateTimeOffset now, string updatedBy)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Name = draft.Name.Trim();
        LegalName = Trimmed(draft.LegalName);
        LogoDataUrl = draft.LogoDataUrl;
        TimeZone = draft.TimeZone.Trim();
        Locale = draft.Locale.Trim();
        AddressLine1 = draft.Address is { } a ? a.Line1.Trim() : null;
        AddressLine2 = Trimmed(draft.Address?.Line2);
        AddressCity = draft.Address is { } a2 ? a2.City.Trim() : null;
        AddressRegion = Trimmed(draft.Address?.Region);
        AddressPostalCode = draft.Address is { } a3 ? a3.PostalCode.Trim() : null;
        AddressCountry = draft.Address is { } a4 ? a4.Country.Trim() : null;
        PrimaryContactName = draft.PrimaryContact is { } p ? p.Name.Trim() : null;
        PrimaryContactEmail = draft.PrimaryContact is { } p2 ? p2.Email.Trim() : null;
        PrimaryContactPhone = Trimmed(draft.PrimaryContact?.Phone);
        SupportContactName = draft.SupportContact is { } s ? s.Name.Trim() : null;
        SupportContactEmail = draft.SupportContact is { } s2 ? s2.Email.Trim() : null;
        SupportContactPhone = Trimmed(draft.SupportContact?.Phone);
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }



    private static string? Trimmed(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
