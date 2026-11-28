// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Secrets;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Integrity;

/// <summary>
/// <see cref="IIntegritySigner"/> with HMAC-SHA256 (E10.4). The key is created on first use (32 random
/// bytes), stored in <c>wms.integrity_key</c> protected under its own Data Protection purpose, and cached
/// in memory; every instance sharing the ring reads the same key. Failed verifications are logged at Error
/// with the table and id, never the content, and counted for the verification job and for tests.
/// </summary>
public sealed partial class IntegritySigner : IIntegritySigner, IDisposable
{
    /// <summary>
    /// The Data Protection purpose of the key material.
    /// </summary>
    public const string Purpose = "Wolfgang.Wms.Integrity.v1";



    // Held for the key transaction: SQL Server blocks every other reader and writer of the table, PostgreSQL every
    // other writer (a reader sees no key and its insert then waits). The lock is self-conflicting on both.
    private const string LockSqlServer = "SELECT COUNT(*) FROM wms.integrity_key WITH (TABLOCKX, HOLDLOCK)";
    private const string LockPostgreSql = "LOCK TABLE wms.integrity_key IN SHARE ROW EXCLUSIVE MODE";



    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<IntegritySigner> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private byte[]? _key;
    private long _failures;



    /// <summary>
    /// Creates the signer.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public IntegritySigner(IServiceScopeFactory scopes, ILogger<IntegritySigner> logger)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <summary>
    /// How many verifications have failed since this instance started.
    /// </summary>
    public long Failures => Interlocked.Read(ref _failures);



    /// <inheritdoc/>
    public async Task<string> SignAsync(string content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var key = await KeyAsync(cancellationToken).ConfigureAwait(false);
        return Sign(key, content);
    }



    private static string Sign(byte[] key, string content)
    {
        return Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(content)));
    }



    /// <inheritdoc/>
    public async Task<bool> IsValidAsync(ISignedEntity entity, string table, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        var expected = await SignAsync(Message(entity), cancellationToken).ConfigureAwait(false);
        var actual = entity.Signature ?? string.Empty;
        if (actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected)))
        {
            return true;
        }

        Interlocked.Increment(ref _failures);
        LogIntegrityFailure(_logger, table, entity.Id);
        return false;
    }



    /// <inheritdoc/>
    public async Task SignAllAsync(IEnumerable<ISignedEntity> entities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);

        foreach (var entity in entities)
        {
            if (entity.Id == 0)
            {
                throw new InvalidOperationException("A signed row is signed once its id is known; save it first (the interceptor signs a new row after its insert).");
            }

            entity.Signature = await SignAsync(Message(entity), cancellationToken).ConfigureAwait(false);
        }
    }



    /// <summary>
    /// What is signed for a row: its id, a newline, then its <see cref="ISignedEntity.CanonicalContent"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> is null.</exception>
    public static string Message(ISignedEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity.Id.ToString(CultureInfo.InvariantCulture) + "\n" + entity.CanonicalContent();
    }



    /// <inheritdoc/>
    public void Dispose()
    {
        _gate.Dispose();
    }



    /// <summary>
    /// Loads the key, creating it when the installation has none yet. Creation and the signing of every
    /// existing security row (the upgrade backfill) commit in one transaction that holds a lock on
    /// <c>wms.integrity_key</c>: a crash leaves no key, so the next start backfills again, and an instance
    /// starting at the same moment waits and then reads the key. True when it was created now.
    /// </summary>
    public async Task<bool> EnsureKeyAsync(CancellationToken cancellationToken)
    {
        if (_key is not null)
        {
            return false;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _key) is not null)   // another caller loaded it while this one waited
            {
                return false;
            }

            using var scope = _scopes.CreateScope();
            var strategy = scope.ServiceProvider.GetRequiredService<WmsDbContext>().Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(LoadOrCreateAsync, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _key = null;   // a key from a transaction that did not commit is never used
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }



    private async Task<bool> LoadOrCreateAsync(CancellationToken cancellationToken)
    {
        _key = null;   // a retried attempt starts over
        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);   // resolved late: the ring is a host concern
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await context.Database.ExecuteSqlRawAsync(context.Database.IsNpgsql() ? LockPostgreSql : LockSqlServer, cancellationToken).ConfigureAwait(false);

            var row = await context.Set<IntegrityKey>().OrderBy(k => k.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (row is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _key = Convert.FromBase64String(protector.Unprotect(ProtectedText.Unwrap(row.ProtectedKey)));
                return false;
            }

            var key = RandomNumberGenerator.GetBytes(32);
            var created = new IntegrityKey { ProtectedKey = ProtectedText.Wrap(protector.Protect(Convert.ToBase64String(key))), CreatedAt = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow() };
            await context.Set<IntegrityKey>().AddAsync(created, cancellationToken).ConfigureAwait(false);
            _key = key;   // the backfill's save signs with it (the interceptor reads the cached key)
            var signed = await BackfillAsync(context, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            LogKeyCreated(_logger, signed);
            return true;
        }
        finally
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
    }



    /// <summary>
    /// Signs every security row as it stands (E10.4, the upgrade path: nothing could have been validly signed
    /// without the key) and saves them with the new key row.
    /// </summary>
    private async Task<int> BackfillAsync(WmsDbContext context, CancellationToken cancellationToken)
    {
        var users = await context.Users.ToListAsync(cancellationToken).ConfigureAwait(false);
        var roles = await context.Roles.Include(r => r.Permissions).ToListAsync(cancellationToken).ConfigureAwait(false);
        var assignments = await context.UserRoles.ToListAsync(cancellationToken).ConfigureAwait(false);
        var mappings = await context.GroupRoleMappings.ToListAsync(cancellationToken).ConfigureAwait(false);
        var pending = users.Cast<ISignedEntity>().Concat(roles).Concat(assignments).Concat(mappings).ToList();
        await SignAllAsync(pending, cancellationToken).ConfigureAwait(false);
        foreach (var entity in pending)
        {
            context.Entry(entity).Property(nameof(ISignedEntity.Signature)).IsModified = true;   // an unchanged signature still writes
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pending.Count;
    }



    private async Task<byte[]> KeyAsync(CancellationToken cancellationToken)
    {
        await EnsureKeyAsync(cancellationToken).ConfigureAwait(false);
        return _key!;
    }



    [LoggerMessage(Level = LogLevel.Error, Message = "Integrity failure: {Table} row {Id} does not match its signature; the row is not honoured. Investigate direct database changes.")]
    private static partial void LogIntegrityFailure(ILogger logger, string table, long id);



    [LoggerMessage(Level = LogLevel.Warning, Message = "Integrity key created in wms.integrity_key and {Count} existing security rows signed as they stand; back it up with the database and the key ring. Expected once per database: on any other start, treat it as an incident.")]
    private static partial void LogKeyCreated(ILogger logger, int count);
}
