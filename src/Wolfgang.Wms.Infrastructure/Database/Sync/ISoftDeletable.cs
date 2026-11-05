// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Database.Sync;

/// <summary>
/// A synced master table that soft-deletes (E5.3): a delete sets <c>deleted_at</c> instead of removing the
/// row, so the deletion appears in the next delta; the retention job hard-deletes rows soft-deleted longer
/// ago than the maximum device-offline window. Reads filter deleted rows out by default; delta queries
/// include them. Transactional picking tables never implement this: they are not synced by row version.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// When the row was deleted (UTC), or null while it lives.
    /// </summary>
    DateTimeOffset? DeletedAt { get; }
}
