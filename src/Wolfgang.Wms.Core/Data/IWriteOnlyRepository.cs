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
    /// Stages an aggregate for deletion.
    /// </summary>
    void Remove(TAggregate aggregate);
}
