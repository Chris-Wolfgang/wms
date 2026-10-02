// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Database.Conventions;

/// <summary>
/// Timestamps are stored at millisecond precision on both providers (E3.4). Values are truncated, not
/// rounded, on the way in, so the value in memory after a truncation equals the value read back and a
/// signature over it (E10.4) is the same before the write and after the read.
/// </summary>
public static class Timestamps
{
    /// <summary>
    /// The value without its sub-millisecond ticks, as UTC.
    /// </summary>
    public static DateTimeOffset TruncateToMillisecond(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return new DateTimeOffset(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond), TimeSpan.Zero);
    }
}
