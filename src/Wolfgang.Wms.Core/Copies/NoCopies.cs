// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Core.Zones;

namespace Wolfgang.Wms.Core.Copies;

/// <summary>
/// The copier of a host without a database (E16.5): every call answers <see cref="CopyErrorCodes.Unavailable"/>.
/// </summary>
public sealed class NoCopies : ICopies
{
    /// <inheritdoc/>
    public Task<SiteInfo> CopySiteAsync(long siteId, SiteCopyRequest request, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<ZoneInfo> CopyZoneAsync(long siteId, long zoneId, ZoneCopyRequest request, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<LocationInfo> CopyLocationAsync(long siteId, long locationId, LocationCopyRequest request, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<IReadOnlyList<LocationInfo>> CopyLocationRangeAsync(long siteId, LocationRangeCopyRequest request, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    private static CopyException Unavailable()
    {
        return new CopyException(CopyErrorCodes.Unavailable, "The database is not configured; nothing can be copied.");
    }
}
