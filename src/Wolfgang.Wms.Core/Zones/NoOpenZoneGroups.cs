// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Zones;

/// <summary>
/// <see cref="IOpenZoneGroups"/> before tote entry exists (E26): no zone has open groups.
/// </summary>
public sealed class NoOpenZoneGroups : IOpenZoneGroups
{
    /// <inheritdoc/>
    public Task<int> CountOpenAsync(long zoneId, CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }
}
