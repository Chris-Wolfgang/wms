// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Caching;

/// <summary>
/// Reads the change stamp of a set of entity types (E1.12, ADR 0003): the highest <c>row_version</c> and
/// the row count across their tables. One cheap query answers "did anything in these tables change?",
/// which is the only invalidation signal the per-instance caches use. Callers name entity types, never
/// tables: the storage names stay in Infrastructure, which implements this per provider and maps each type
/// through the model.
/// </summary>
public interface IRowVersionSource
{
    /// <summary>
    /// The change stamp across the tables of <paramref name="entityTypes"/>: the highest row version (0 when
    /// they are all empty) and the total row count.
    /// </summary>
    Task<RowVersionStamp> GetStampAsync(IReadOnlyCollection<Type> entityTypes, CancellationToken cancellationToken);
}
