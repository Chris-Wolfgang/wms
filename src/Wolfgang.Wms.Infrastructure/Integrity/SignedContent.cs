// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Wolfgang.Wms.Infrastructure.Database.Conventions;

namespace Wolfgang.Wms.Infrastructure.Integrity;

/// <summary>
/// Helpers for a stable <see cref="ISignedEntity.CanonicalContent"/> (E10.4).
/// </summary>
public static class SignedContent
{
    /// <summary>
    /// A timestamp as stored: UTC, truncated to the millisecond exactly as the converters truncate it on the
    /// way in, so the content is the same before the write and after the read.
    /// </summary>
    public static string Timestamp(DateTimeOffset? value)
    {
        return value is { } instant
            ? Timestamps.TruncateToMillisecond(instant).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)
            : string.Empty;
    }
}
