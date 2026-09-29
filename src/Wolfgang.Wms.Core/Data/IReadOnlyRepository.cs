// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Data;

/// <summary>
/// Read side of a repository for one aggregate (E1.11): repositories are per aggregate, not per table,
/// and the specific interface (<c>ISkuRepository</c>, <c>IZoneGroupRepository</c>, …) inherits this base
/// so master-data reads come for free. No <c>IQueryable</c>, no context, no expression trees leak out.
/// </summary>
/// <typeparam name="TAggregate">Aggregate root type.</typeparam>
/// <typeparam name="TId">Identifier type of the aggregate.</typeparam>
public interface IReadOnlyRepository<TAggregate, in TId>
    where TAggregate : class
    where TId : notnull
{
    /// <summary>
    /// The aggregate with the given identifier, or null when none exists in the caller's site scope.
    /// </summary>
    Task<TAggregate?> FindAsync(TId id, CancellationToken cancellationToken);



    /// <summary>
    /// True when an aggregate with the given identifier exists in the caller's site scope.
    /// </summary>
    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken);



    /// <summary>
    /// Every aggregate in the caller's site scope. Intended for master data; large sets use
    /// <see cref="ISearchableRepository{TAggregate, TCriteria}"/> with paging criteria.
    /// </summary>
    Task<IReadOnlyList<TAggregate>> ListAsync(CancellationToken cancellationToken);
}
