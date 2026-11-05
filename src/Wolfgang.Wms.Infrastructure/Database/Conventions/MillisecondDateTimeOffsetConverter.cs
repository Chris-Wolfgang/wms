// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Wolfgang.Wms.Infrastructure.Database.Conventions;

/// <summary>
/// PostgreSQL stores every timestamp as <c>timestamptz(3)</c>; Npgsql maps a UTC <see cref="DateTimeOffset"/>
/// natively, so this converter only truncates to the millisecond on the way in (E3.4, E10.4): the server
/// would otherwise round, and a value near a millisecond boundary would read back different from what
/// was signed.
/// </summary>
public sealed class MillisecondDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    /// <summary>
    /// Creates the converter.
    /// </summary>
    public MillisecondDateTimeOffsetConverter()
        : base(value => Timestamps.TruncateToMillisecond(value), value => value)
    {
    }
}
