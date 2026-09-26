// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Data;

/// <summary>
/// Search side of a repository (E1.11): callers pass a criteria object the repository knows how to apply.
/// Criteria are plain records, never expression trees, so the contract is AOT-safe and never exposes the
/// query provider.
/// </summary>
/// <typeparam name="TAggregate">Aggregate root type.</typeparam>
/// <typeparam name="TCriteria">Criteria record for this aggregate (filters, paging, ordering).</typeparam>
public interface ISearchableRepository<TAggregate, in TCriteria>
    where TAggregate : class
    where TCriteria : notnull
{
    /// <summary>
    /// Aggregates in the caller's site scope matching <paramref name="criteria"/>.
    /// </summary>
    Task<IReadOnlyList<TAggregate>> SearchAsync(TCriteria criteria, CancellationToken cancellationToken);
}
