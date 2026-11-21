// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// One paid maintenance period of a key (E79.5): a release is installable when its release date falls
/// inside a covered period.
/// </summary>
/// <param name="From">The first covered day.</param>
/// <param name="To">The last covered day.</param>
public sealed record CoveragePeriod(DateOnly From, DateOnly To)
{
    /// <summary>
    /// True when <paramref name="day"/> lies in the period.
    /// </summary>
    public bool Contains(DateOnly day)
    {
        return day >= From && day <= To;
    }
}
