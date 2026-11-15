// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Zones;

/// <summary>
/// What a zone is for (E16.2).
/// </summary>
public enum ZoneType
{
    /// <summary>A picking area on the conveyor route; totes stop here for picks.</summary>
    Pick,

    /// <summary>Bulk storage that feeds the pick zones; no tote picking.</summary>
    Bulk,

    /// <summary>
    /// Where exceptions are resolved: weight failures, shorts, adjustments, misdirects. Zero or more per site,
    /// created only in the console or the API (never by ERP or CSV load), with the properties ordinary zones do
    /// not have (<see cref="ResolutionZone"/>).
    /// </summary>
    Resolution,
}
