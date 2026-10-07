// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Copies;

/// <summary>
/// What an administrator sends to copy a range of locations at once (E16.5), such as aisle <c>A</c> to aisle
/// <c>B</c>: every location whose code starts with the prefix is copied with the prefix replaced in its code
/// and barcode (and, optionally, its walk sequence).
/// </summary>
/// <param name="CodePrefixFrom">The prefix the source codes start with.</param>
/// <param name="CodePrefixTo">The replacement prefix for the copies' codes and barcodes.</param>
/// <param name="ZoneId">The zone the copies go to; null to keep each source's zone.</param>
/// <param name="WalkPrefixFrom">Optional: the prefix of the source walk sequences to replace.</param>
/// <param name="WalkPrefixTo">The replacement walk prefix; required when <paramref name="WalkPrefixFrom"/> is given.</param>
public sealed record LocationRangeCopyRequest
(
    string CodePrefixFrom,
    string CodePrefixTo,
    long? ZoneId = null,
    string? WalkPrefixFrom = null,
    string? WalkPrefixTo = null
);
