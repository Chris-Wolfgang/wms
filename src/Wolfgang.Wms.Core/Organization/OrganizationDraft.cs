// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Organization;

/// <summary>
/// Body of <c>POST /organization</c> (the first-run wizard's first step) and <c>PUT /organization</c> (E16.0).
/// </summary>
/// <param name="Name">The name users see: console header, login page, notifications, cards; 1–128 characters.</param>
/// <param name="LegalName">The registered company name; null to use <paramref name="Name"/>.</param>
/// <param name="LogoDataUrl">The logo as a <c>data:image/…</c> URL, at most 256 KB; null for none.</param>
/// <param name="TimeZone">The default time zone (IANA or Windows id), the one sites inherit.</param>
/// <param name="Locale">The default culture name (<c>en-US</c>), the one users inherit.</param>
/// <param name="Address">The postal address; null when not yet known.</param>
/// <param name="PrimaryContact">Who the vendor and the install talk to; null when not yet known.</param>
/// <param name="SupportContact">Who users are pointed at; null to use the primary contact.</param>
public sealed record OrganizationDraft
(
    string Name,
    string? LegalName,
    string? LogoDataUrl,
    string TimeZone,
    string Locale,
    OrganizationAddress? Address,
    OrganizationContact? PrimaryContact,
    OrganizationContact? SupportContact
);
