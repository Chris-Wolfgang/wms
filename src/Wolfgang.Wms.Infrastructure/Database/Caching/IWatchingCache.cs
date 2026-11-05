// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Database.Caching;

/// <summary>
/// A per-instance cache that watches some entity types (ADR 0003): the row-version probe catches writes by
/// other instances, and <see cref="OwnWritesInvalidation"/> drops the cache the moment this instance saves
/// one of those types, whichever store did the saving.
/// </summary>
public interface IWatchingCache
{
    /// <summary>
    /// The entity types whose tables decide what the cache holds.
    /// </summary>
    IReadOnlyCollection<Type> EntityTypes { get; }



    /// <summary>
    /// Drops what is cached so the next read reloads.
    /// </summary>
    void Invalidate();
}
