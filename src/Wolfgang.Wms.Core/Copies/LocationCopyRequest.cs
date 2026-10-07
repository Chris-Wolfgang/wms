// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Copies;

/// <summary>
/// What an administrator sends to copy one location (E16.5): the same properties with a new code and barcode.
/// </summary>
/// <param name="Code">The new bin code, unique within the site.</param>
/// <param name="Barcode">The new barcode, unique within the site.</param>
/// <param name="WalkSequence">The new walk sequence; null to keep the source's.</param>
/// <param name="ZoneId">The zone to copy into; null for the source location's zone.</param>
public sealed record LocationCopyRequest
(
    string Code,
    string Barcode,
    string? WalkSequence = null,
    long? ZoneId = null
);
