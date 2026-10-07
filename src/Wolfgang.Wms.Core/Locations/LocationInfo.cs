// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Locations;

/// <summary>
/// A location as the API reports it (E17.1): one bin in a zone of a site.
/// </summary>
/// <param name="Id">The server-assigned identifier.</param>
/// <param name="SiteId">The site the bin belongs to.</param>
/// <param name="ZoneId">The zone the bin is in.</param>
/// <param name="Code">The bin code, unique within the site.</param>
/// <param name="Barcode">The label on the bin, unique within the site.</param>
/// <param name="WalkSequence">The sortable walk order.</param>
/// <param name="IsPickable">False for a bin never picked from.</param>
/// <param name="IsActive">False once the bin is retired.</param>
/// <param name="UpdatedAt">When the row was last written (UTC).</param>
/// <param name="UpdatedBy">Who last wrote the row.</param>
/// <param name="RowVersion">The row's version (E5.1); the <see cref="Etag"/> is derived from it.</param>
public sealed record LocationInfo
(
    long Id,
    long SiteId,
    long ZoneId,
    string Code,
    string Barcode,
    string WalkSequence,
    bool IsPickable,
    bool IsActive,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long RowVersion
)
{
    /// <summary>The entity tag a client sends back in <c>If-Match</c>.</summary>
    public string Etag => Http.EntityTag.FromRowVersion((ulong)RowVersion).Value;
}
