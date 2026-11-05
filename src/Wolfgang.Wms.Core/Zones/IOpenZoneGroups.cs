// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Zones;

/// <summary>
/// What keeps a zone from being deactivated (E16.2): the zone groups still open in it. Tote entry and zone
/// group completion (E26, E27) supply the stored answer; until then <see cref="NoOpenZoneGroups"/> reports
/// none, so deactivation is never blocked.
/// </summary>
public interface IOpenZoneGroups
{
    /// <summary>
    /// How many zone groups are open (fetched and not yet completed) in a zone.
    /// </summary>
    Task<int> CountOpenAsync(long zoneId, CancellationToken cancellationToken);
}
