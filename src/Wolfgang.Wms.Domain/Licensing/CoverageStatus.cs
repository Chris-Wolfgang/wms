// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// Whether this release is covered by the base key (E79.5).
/// </summary>
public enum CoverageStatus
{
    /// <summary>The free tier: no expiry, ever.</summary>
    Perpetual,

    /// <summary>The release date lies in a paid period.</summary>
    Covered,

    /// <summary>The release date lies outside every paid period: everything in effect stays, increases and upgrades are blocked.</summary>
    Lapsed,
}
