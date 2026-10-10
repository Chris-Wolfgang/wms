// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Http.Paging;

namespace Wolfgang.Wms.Core.Locations;

/// <summary>
/// The locations store of a host without a database (E17.1): every call answers
/// <see cref="LocationErrorCodes.Unavailable"/>.
/// </summary>
public sealed class NoLocations : ILocations
{
    /// <inheritdoc/>
    public Task<Page<LocationInfo>> ListAsync(long siteId, LocationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<LocationInfo> FindAsync(long siteId, long locationId, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<LocationInfo> CreateAsync(long siteId, LocationDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<LocationInfo> UpdateAsync(long siteId, long locationId, LocationDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    private static LocationException Unavailable()
    {
        return new LocationException(LocationErrorCodes.Unavailable, "The database is not configured; there are no locations to read or write.");
    }
}
