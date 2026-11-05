// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Zones;

/// <summary>
/// The zones store of a host without a database (E16.2): every call answers
/// <see cref="ZoneErrorCodes.Unavailable"/>.
/// </summary>
public sealed class NoZones : IZones
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<ZoneInfo>> ListAsync(long siteId, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<ZoneInfo> FindAsync(long siteId, long zoneId, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<ZoneInfo> CreateAsync(long siteId, ZoneDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<ZoneInfo> UpdateAsync(long siteId, long zoneId, ZoneDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    private static ZoneException Unavailable()
    {
        return new ZoneException(ZoneErrorCodes.Unavailable, "The database is not configured; there are no zones to read or write.");
    }
}
