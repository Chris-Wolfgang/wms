// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Locations;

/// <summary>
/// What an administrator (or an import) sends to create or replace a location (E17.1): one bin.
/// </summary>
/// <param name="Code">The bin code, unique within the site: letters, digits, <c>-</c> and <c>_</c>, compared without regard to case (the site's code format, E17.11, generates it from aisle/bay/level/position).</param>
/// <param name="Barcode">The label on the bin, unique within the site; what a picker scans.</param>
/// <param name="ZoneId">The zone the bin is in; a zone of the same site.</param>
/// <param name="WalkSequence">A sortable string: the order a picker walks the bins in. Starts with the zone's walk-order prefix when the zone has one.</param>
/// <param name="IsPickable">False for a bin that holds stock but is never picked from (staging, overflow).</param>
/// <param name="IsActive">False to retire the bin: it stays in history but takes no new stock or tasks.</param>
public sealed record LocationDraft
(
    string Code,
    string Barcode,
    long ZoneId,
    string WalkSequence,
    bool IsPickable = true,
    bool IsActive = true
);
