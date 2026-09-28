// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Data;

/// <summary>
/// Write side of a repository (E1.11). Adds and removals are staged in the current
/// <see cref="IUnitOfWork"/> and persisted by its single <c>SaveChangesAsync</c>; a write outside the
/// caller's site scope is rejected there. Repositories persist aggregates; handlers own orchestration and
/// rules.
/// </summary>
/// <typeparam name="TAggregate">Aggregate root type.</typeparam>
public interface IWriteOnlyRepository<in TAggregate>
    where TAggregate : class
{
    /// <summary>
    /// Stages a new aggregate for insertion.
    /// </summary>
    void Add(TAggregate aggregate);



    /// <summary>
    /// Stages new aggregates for insertion in one call (intake, deposit replay): one change-detection pass
    /// instead of one per aggregate.
    /// </summary>
    void AddRange(IEnumerable<TAggregate> aggregates);



    /// <summary>
    /// Stages an aggregate for deletion. There is no remove-by-id on the base contract: the aggregate is
    /// loaded first so the handler's rules, the concurrency token and the site-scope check at
    /// <c>SaveChangesAsync</c> all see the real row; a repository that needs a bulk delete without loading
    /// adds a business-named method for it.
    /// </summary>
    void Remove(TAggregate aggregate);



    /// <summary>
    /// Stages aggregates for deletion in one call.
    /// </summary>
    void RemoveRange(IEnumerable<TAggregate> aggregates);
}
