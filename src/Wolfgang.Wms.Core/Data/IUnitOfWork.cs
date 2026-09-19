// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Data;

/// <summary>
/// The thin seam over the database context that handlers use (E1.11). One unit of work per request or job;
/// one <see cref="SaveChangesAsync"/> per operation; explicit transactions only for the listed multi-step
/// operations (deposit replay, marriage, intake, settings cascade, lease release/expiry) and never around
/// non-database I/O. There is deliberately no <c>Set&lt;T&gt;()</c> and no change-tracker access: handlers
/// reach data through repositories, never through the context.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists every change made through the repositories in this unit of work.
    /// </summary>
    /// <returns>The number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);



    /// <summary>
    /// Runs <paramref name="operation"/> inside one database transaction. The delegate form lets the
    /// implementation roll back on failure and retry on transient errors without the caller knowing;
    /// the operation must not perform non-database I/O.
    /// </summary>
    /// <typeparam name="TResult">Result of the operation.</typeparam>
    Task<TResult> ExecuteInTransactionAsync<TResult>
    (
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken
    );
}
