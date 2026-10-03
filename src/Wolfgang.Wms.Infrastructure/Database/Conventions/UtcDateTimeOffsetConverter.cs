// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Wolfgang.Wms.Infrastructure.Database.Conventions;

/// <summary>
/// SQL Server stores every timestamp as UTC <c>datetime2(3)</c> (E3.4) rather than <c>datetimeoffset</c>: the
/// offset is always zero in this product (E1.14), and <c>datetime2</c> is smaller and indexes better. Values
/// go in as <see cref="DateTimeOffset.UtcDateTime"/> and come back as UTC <see cref="DateTimeOffset"/>.
/// PostgreSQL needs no converter: Npgsql maps a UTC <see cref="DateTimeOffset"/> to <c>timestamptz</c>.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTime>
{
    /// <summary>
    /// Creates the converter.
    /// </summary>
    public UtcDateTimeOffsetConverter()
        : base(value => value.UtcDateTime, value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)))
    {
    }
}
