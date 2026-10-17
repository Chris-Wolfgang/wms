// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wolfgang.Wms.Infrastructure.Identity;

namespace Wolfgang.Wms.Infrastructure.Integrity;

/// <summary>
/// Signs every changed <see cref="ISignedEntity"/> before it is saved and every added one right after its
/// insert, when its id (part of the signature) exists (E10.4), and re-signs a role whose permission rows
/// changed, so the application never leaves a stale-signed security row.
/// </summary>
public sealed class IntegritySigningInterceptor : SaveChangesInterceptor
{
    private readonly IIntegritySigner _signer;
    private readonly ConditionalWeakTable<DbContext, List<ISignedEntity>> _unsigned = new();   // per save: the rows to sign after the insert



    /// <summary>
    /// Creates the interceptor.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="signer"/> is null.</exception>
    public IntegritySigningInterceptor(IIntegritySigner signer)
    {
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
    }



    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            var pending = Pending(context);
            if (pending.Count > 0)
            {
                // A new row has no id until the insert, and the id is signed: it is saved unsigned (it fails
                // verification meanwhile) and signed by a second save as soon as the insert has assigned it.
                var added = pending.Where(e => context.Entry(e).State == EntityState.Added).ToList();
                foreach (var entity in added)
                {
                    entity.Signature = null;
                }

                if (added.Count > 0)
                {
                    _unsigned.AddOrUpdate(context, added);
                }

                await _signer.SignAllAsync(pending.Except(added), cancellationToken).ConfigureAwait(false);
                context.ChangeTracker.DetectChanges();   // the save detected changes before calling here
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }



    /// <summary>
    /// Signs the rows the save inserted, now that their ids exist, and saves the signatures. A failure
    /// between the two saves leaves the new rows unsigned: they fail verification and are not honoured.
    /// </summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context && _unsigned.TryGetValue(context, out var added))
        {
            _unsigned.Remove(context);
            await _signer.SignAllAsync(added, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }



    /// <inheritdoc/>
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            _unsigned.Remove(context);
        }

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }



    /// <summary>
    /// The synchronous save is not supported: signing loads the key asynchronously, and the product only
    /// saves asynchronously. Refusing is safer than saving unsigned rows.
    /// </summary>
    /// <exception cref="NotSupportedException">Always, when the save touches a signed row.</exception>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context && Pending(context).Count > 0)
        {
            throw new NotSupportedException("Signed rows must be saved with SaveChangesAsync.");
        }

        return base.SavingChanges(eventData, result);
    }



    /// <summary>
    /// The signed rows a save touches: added or modified signed entities, plus the roles whose permission
    /// rows were added or removed (the role row itself may be unchanged).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// An added signed row references a principal added in the same save: its foreign key is unknown until
    /// the principal is inserted, so the signature would be wrong. Save the principal first.
    /// </exception>
    public static IReadOnlyList<ISignedEntity> Pending(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pending = new HashSet<ISignedEntity>(ReferenceEqualityComparer.Instance);
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is ISignedEntity signed && entry.State is EntityState.Added or EntityState.Modified)
            {
                var temporary = entry.Properties.FirstOrDefault(p => p.IsTemporary && p.Metadata.IsForeignKey());
                if (temporary is not null)
                {
                    throw new InvalidOperationException($"{entry.Metadata.DisplayName()}.{temporary.Metadata.Name} is not known until its principal is inserted; save the principal first so the row is signed with the real key.");
                }

                pending.Add(signed);
            }

            if (entry.Entity is RolePermission permission && entry.State is EntityState.Added or EntityState.Deleted or EntityState.Modified)
            {
                var role = context.ChangeTracker.Entries<Role>().FirstOrDefault(r => r.Entity.Permissions.Contains(permission) || r.Entity.Id == permission.RoleId);
                if (role is not null && role.State is EntityState.Unchanged or EntityState.Modified)
                {
                    pending.Add(role.Entity);
                }
            }
        }

        return [.. pending];
    }
}
