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
    /// Runs <paramref name="operation"/> inside one database transaction. The delegate form is deliberate:
    /// the transaction's scope is the delegate, so commit and rollback are bound to it (an exception rolls
    /// back, there is no flag to forget to clear), the operation may call <see cref="SaveChangesAsync"/> more
    /// than once inside the same transaction, and the provider's execution strategy can replay the whole
    /// delegate on a transient error, which an ambient "start a transaction on the next save" flag cannot.
    /// A single <see cref="SaveChangesAsync"/> is already atomic on its own, so this is only for the listed
    /// multi-step operations, and the operation must not perform non-database I/O.
    /// </summary>
    /// <typeparam name="TResult">Result of the operation.</typeparam>
    Task<TResult> ExecuteInTransactionAsync<TResult>
    (
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken
    );



    /// <summary>
    /// Runs <paramref name="operation"/> inside one database transaction, for operations without a result.
    /// Same contract as <see cref="ExecuteInTransactionAsync{TResult}"/>.
    /// </summary>
    Task ExecuteInTransactionAsync
    (
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken
    );
}
