// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Zones;

/// <summary>
/// The zones store (E16.2): the picking areas, bulk storage and resolution zones within each site.
/// <c>AddWmsDatabase</c> replaces the placeholder with the stored one.
/// </summary>
public interface IZones
{
    /// <summary>
    /// Every zone of a site, active and retired, in code order.
    /// </summary>
    /// <exception cref="ZoneException"><see cref="ZoneErrorCodes.SiteNotFound"/>.</exception>
    Task<IReadOnlyList<ZoneInfo>> ListAsync(long siteId, CancellationToken cancellationToken);



    /// <summary>
    /// One zone of a site by id.
    /// </summary>
    /// <exception cref="ZoneException"><see cref="ZoneErrorCodes.SiteNotFound"/> or <see cref="ZoneErrorCodes.NotFound"/>.</exception>
    Task<ZoneInfo> FindAsync(long siteId, long zoneId, CancellationToken cancellationToken);



    /// <summary>
    /// Creates a zone in a site from a valid draft.
    /// </summary>
    /// <exception cref="ZoneException"><see cref="ZoneErrorCodes.SiteNotFound"/>, <see cref="ZoneErrorCodes.Invalid"/> or <see cref="ZoneErrorCodes.CodeTaken"/>.</exception>
    Task<ZoneInfo> CreateAsync(long siteId, ZoneDraft draft, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Replaces a zone's details. Deactivating a zone (<see cref="ZoneDraft.IsActive"/> false on an active zone)
    /// is refused while <see cref="IOpenZoneGroups"/> reports open groups in it.
    /// </summary>
    /// <exception cref="ZoneException"><see cref="ZoneErrorCodes.SiteNotFound"/>, <see cref="ZoneErrorCodes.NotFound"/>, <see cref="ZoneErrorCodes.Invalid"/>, <see cref="ZoneErrorCodes.CodeTaken"/> or <see cref="ZoneErrorCodes.HasOpenGroups"/>.</exception>
    Task<ZoneInfo> UpdateAsync(long siteId, long zoneId, ZoneDraft draft, string updatedBy, CancellationToken cancellationToken);
}
