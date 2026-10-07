// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Sites;

/// <summary>
/// A site as the API reports it (E16.1): one warehouse, the operational level pickers, role assignments
/// and settings are scoped to.
/// </summary>
/// <param name="Id">The server-assigned identifier; what <c>siteId</c> route values and <c>X-Wms-Site</c> carry.</param>
/// <param name="Code">The short unique code.</param>
/// <param name="Name">The name users see.</param>
/// <param name="TimeZone">The site's time zone id.</param>
/// <param name="IsActive">False once the site is retired.</param>
/// <param name="UpdatedAt">When the row was last written (UTC).</param>
/// <param name="UpdatedBy">Who last wrote the row.</param>
/// <param name="RowVersion">The row's version (E5.1); the <see cref="Etag"/> is derived from it.</param>
public sealed record SiteInfo
(
    long Id,
    string Code,
    string Name,
    string TimeZone,
    bool IsActive,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long RowVersion
)
{
    /// <summary>The entity tag a client sends back in <c>If-Match</c>.</summary>
    public string Etag => Http.EntityTag.FromRowVersion((ulong)RowVersion).Value;
}
