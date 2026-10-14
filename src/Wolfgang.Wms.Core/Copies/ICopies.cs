// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Core.Zones;

namespace Wolfgang.Wms.Core.Copies;

/// <summary>
/// Copies of master data (E16.5): a site, a zone, a location or a range of locations, each an API operation
/// audited as creates that carry a <c>copiedFromId</c>. <c>AddWmsDatabase</c> replaces the placeholder with the
/// stored one.
/// </summary>
public interface ICopies
{
    /// <summary>
    /// Copies a site with the chosen parts; the copy opens in the wizard for what must differ.
    /// </summary>
    /// <exception cref="CopyException"><see cref="CopyErrorCodes.NotFound"/>, <see cref="CopyErrorCodes.Invalid"/>, <see cref="CopyErrorCodes.CodeTaken"/> or <see cref="CopyErrorCodes.BarcodeTaken"/>.</exception>
    Task<SiteInfo> CopySiteAsync(long siteId, SiteCopyRequest request, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Copies a zone within or across sites with the chosen parts.
    /// </summary>
    /// <exception cref="CopyException"><see cref="CopyErrorCodes.NotFound"/>, <see cref="CopyErrorCodes.Invalid"/>, <see cref="CopyErrorCodes.CodeTaken"/> or <see cref="CopyErrorCodes.BarcodeTaken"/>.</exception>
    Task<ZoneInfo> CopyZoneAsync(long siteId, long zoneId, ZoneCopyRequest request, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Copies one location with a new code and barcode.
    /// </summary>
    /// <exception cref="CopyException"><see cref="CopyErrorCodes.NotFound"/>, <see cref="CopyErrorCodes.Invalid"/>, <see cref="CopyErrorCodes.CodeTaken"/> or <see cref="CopyErrorCodes.BarcodeTaken"/>.</exception>
    Task<LocationInfo> CopyLocationAsync(long siteId, long locationId, LocationCopyRequest request, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Copies every location whose code starts with a prefix, substituting the prefix; all or nothing.
    /// </summary>
    /// <exception cref="CopyException"><see cref="CopyErrorCodes.NotFound"/>, <see cref="CopyErrorCodes.Invalid"/>, <see cref="CopyErrorCodes.CodeTaken"/> or <see cref="CopyErrorCodes.BarcodeTaken"/>.</exception>
    Task<IReadOnlyList<LocationInfo>> CopyLocationRangeAsync(long siteId, LocationRangeCopyRequest request, string updatedBy, CancellationToken cancellationToken);
}
