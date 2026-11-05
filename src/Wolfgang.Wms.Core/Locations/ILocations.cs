// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Http.Paging;

namespace Wolfgang.Wms.Core.Locations;

/// <summary>
/// The locations store (E17.1): the bins within each site's zones. <c>AddWmsDatabase</c> replaces the
/// placeholder with the stored one.
/// </summary>
public interface ILocations
{
    /// <summary>
    /// One page of a site's locations, active and retired, in the requested sort.
    /// </summary>
    /// <exception cref="LocationException"><see cref="LocationErrorCodes.SiteNotFound"/>.</exception>
    Task<Page<LocationInfo>> ListAsync(long siteId, LocationQuery query, CancellationToken cancellationToken);



    /// <summary>
    /// One location of a site by id.
    /// </summary>
    /// <exception cref="LocationException"><see cref="LocationErrorCodes.SiteNotFound"/> or <see cref="LocationErrorCodes.NotFound"/>.</exception>
    Task<LocationInfo> FindAsync(long siteId, long locationId, CancellationToken cancellationToken);



    /// <summary>
    /// Creates a location in a site from a valid draft.
    /// </summary>
    /// <exception cref="LocationException"><see cref="LocationErrorCodes.SiteNotFound"/>, <see cref="LocationErrorCodes.Invalid"/>, <see cref="LocationErrorCodes.ZoneNotFound"/>, <see cref="LocationErrorCodes.CodeTaken"/> or <see cref="LocationErrorCodes.BarcodeTaken"/>.</exception>
    Task<LocationInfo> CreateAsync(long siteId, LocationDraft draft, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Replaces a location's details.
    /// </summary>
    /// <exception cref="LocationException"><see cref="LocationErrorCodes.SiteNotFound"/>, <see cref="LocationErrorCodes.NotFound"/>, <see cref="LocationErrorCodes.Invalid"/>, <see cref="LocationErrorCodes.ZoneNotFound"/>, <see cref="LocationErrorCodes.CodeTaken"/> or <see cref="LocationErrorCodes.BarcodeTaken"/>.</exception>
    Task<LocationInfo> UpdateAsync(long siteId, long locationId, LocationDraft draft, string updatedBy, CancellationToken cancellationToken);
}
