// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Sites;

/// <summary>
/// What an administrator sends to create or replace a site (E16.1).
/// </summary>
/// <param name="Code">The short unique code (<c>DC1</c>, <c>HAM-01</c>): letters, digits, <c>-</c> and <c>_</c>, compared without regard to case.</param>
/// <param name="Name">The name users see.</param>
/// <param name="TimeZone">An IANA or Windows time zone id the host knows; what the site's clocks, cut-offs and digests use. Null or blank to take the organisation's default time zone (E16.4); required until the organisation exists.</param>
/// <param name="IsActive">False to retire the site: it stays in reports and history but takes no new work. Refused while open releases exist.</param>
public sealed record SiteDraft
(
    string Code,
    string Name,
    string? TimeZone,
    bool IsActive = true
);
