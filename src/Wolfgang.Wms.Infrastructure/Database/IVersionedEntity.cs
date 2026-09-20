// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// An entity whose table carries <c>row_version</c> (E5.1): a value from the database's single sequence,
/// assigned by a column default on insert and overwritten by an update trigger, so it is unique and
/// monotonic across the whole database whoever writes the row. It is the concurrency token (E5.2), the
/// <c>ETag</c> (E1.12) and the sync watermark for deltas and manifests (E5.3, E5.4). Never set it in code.
/// </summary>
public interface IVersionedEntity
{
    /// <summary>
    /// The row version as last read; the database assigns it.
    /// </summary>
    long RowVersion { get; }
}
