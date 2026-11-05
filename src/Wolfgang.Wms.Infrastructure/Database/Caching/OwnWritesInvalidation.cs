// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Wolfgang.Wms.Infrastructure.Database.Caching;

/// <summary>
/// Drops every <see cref="IWatchingCache"/> whose entity types this instance has just written (ADR 0003), so a
/// store that sets a column another cache depends on (a password change moving "sessions valid after", a
/// zone changing site) is seen at once here rather than at the next row-version probe. The types are
/// captured before the save, and the caches are dropped once the change is visible: after the save when it
/// ran on its own, after the commit when it ran inside a transaction.
/// </summary>
public sealed class OwnWritesInvalidation : ISaveChangesInterceptor, IDbTransactionInterceptor
{
    private readonly IReadOnlyList<IWatchingCache> _caches;
    private readonly ConditionalWeakTable<DbContext, HashSet<Type>> _pending = [];



    /// <summary>
    /// Creates the interceptor over <paramref name="caches"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="caches"/> is null.</exception>
    public OwnWritesInvalidation(IEnumerable<IWatchingCache> caches)
    {
        ArgumentNullException.ThrowIfNull(caches);
        _caches = caches.ToList();
    }



    /// <inheritdoc/>
    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Capture(eventData);
        return result;
    }



    /// <inheritdoc/>
    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Capture(eventData);
        return ValueTask.FromResult(result);
    }



    /// <inheritdoc/>
    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Saved(eventData);
        return result;
    }



    /// <inheritdoc/>
    public ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Saved(eventData);
        return ValueTask.FromResult(result);
    }



    /// <inheritdoc/>
    public void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Forget(eventData);
    }



    /// <inheritdoc/>
    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Forget(eventData);
        return Task.CompletedTask;
    }



    /// <inheritdoc/>
    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Flush(eventData);
    }



    /// <inheritdoc/>
    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Flush(eventData);
        return Task.CompletedTask;
    }



    /// <inheritdoc/>
    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Forget(eventData);
    }



    /// <inheritdoc/>
    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Forget(eventData);
        return Task.CompletedTask;
    }



    private void Capture(DbContextEventData eventData)
    {
        if (eventData.Context is not { } context)
        {
            return;
        }

        var pending = _pending.GetOrCreateValue(context);
        foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            pending.Add(entry.Metadata.ClrType);
        }
    }



    private void Saved(DbContextEventData eventData)
    {
        if (eventData.Context is { Database.CurrentTransaction: null })
        {
            Flush(eventData);
        }
    }



    private void Flush(DbContextEventData eventData)
    {
        if (eventData.Context is not { } context || !_pending.TryGetValue(context, out var pending))
        {
            return;
        }

        _pending.Remove(context);
        foreach (var cache in _caches.Where(c => c.EntityTypes.Any(pending.Contains)))
        {
            cache.Invalidate();
        }
    }



    private void Forget(DbContextEventData eventData)
    {
        if (eventData.Context is { } context)
        {
            _pending.Remove(context);
        }
    }
}
