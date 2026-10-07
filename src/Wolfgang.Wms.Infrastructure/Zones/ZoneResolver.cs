// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Zones;

/// <summary>
/// A row of <c>core.zone_resolver</c> (E16.2): one user assigned to resolve in a resolution zone.
/// </summary>
public sealed class ZoneResolver
{
    /// <summary>The server-assigned identifier.</summary>
    public long Id { get; set; }

    /// <summary>The resolution zone.</summary>
    public long ZoneId { get; set; }

    /// <summary>The assigned user (<c>core.user</c>).</summary>
    public long UserId { get; set; }
}
