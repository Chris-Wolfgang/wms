// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Caching;

/// <summary>
/// Reads the highest <c>row_version</c> across a set of tables (E1.12, ADR 0003). One cheap query answers
/// "did anything in these tables change?", which is the only invalidation signal the per-instance caches use.
/// Infrastructure implements it per provider.
/// </summary>
public interface IRowVersionSource
{
    /// <summary>
    /// The highest row version among <paramref name="tables"/>, or 0 when they are all empty.
    /// </summary>
    Task<ulong> GetMaxRowVersionAsync(IReadOnlyCollection<string> tables, CancellationToken cancellationToken);
}
