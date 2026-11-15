// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Organization;

/// <summary>
/// The organisation as the API and the console see it (E16.0): the one row per install that is the top of
/// the settings cascade, the name on the console header, the licensee the keys name, and the install's name
/// in multi-install monitoring.
/// </summary>
/// <param name="Id">The server-assigned identifier.</param>
/// <param name="Name">The name users see.</param>
/// <param name="LegalName">The registered company name; null to use <paramref name="Name"/>.</param>
/// <param name="LogoDataUrl">The logo as a <c>data:image/…</c> URL; null for none.</param>
/// <param name="TimeZone">The default time zone id.</param>
/// <param name="Locale">The default culture name.</param>
/// <param name="Address">The postal address; null when not yet known.</param>
/// <param name="PrimaryContact">Who the vendor and the install talk to; null when not yet known.</param>
/// <param name="SupportContact">Who users are pointed at; null to use the primary contact.</param>
/// <param name="UpdatedAt">When the row was last written (UTC).</param>
/// <param name="UpdatedBy">Who last wrote it.</param>
/// <param name="RowVersion">The row's version (the entity tag on updates).</param>
public sealed record OrganizationInfo
(
    long Id,
    string Name,
    string? LegalName,
    string? LogoDataUrl,
    string TimeZone,
    string Locale,
    OrganizationAddress? Address,
    OrganizationContact? PrimaryContact,
    OrganizationContact? SupportContact,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long RowVersion
)
{
    /// <summary>
    /// The entity tag for <c>If-Match</c>.
    /// </summary>
    public string Etag => Http.EntityTag.FromRowVersion((ulong)RowVersion).Value;



    /// <summary>
    /// What anyone may see before signing in: the name on the login page and the logo.
    /// </summary>
    public OrganizationPublicInfo ToPublic()
    {
        return new OrganizationPublicInfo(Name, LogoDataUrl);
    }
}
