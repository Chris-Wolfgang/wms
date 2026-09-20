// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

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
        return Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(content)));
    }



    /// <inheritdoc/>
    public async Task<bool> IsValidAsync(ISignedEntity entity, string table, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        var expected = await SignAsync(entity.CanonicalContent(), cancellationToken).ConfigureAwait(false);
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
            entity.Signature = await SignAsync(entity.CanonicalContent(), cancellationToken).ConfigureAwait(false);
        }
    }



    /// <inheritdoc/>
    public void Dispose()
    {
        _gate.Dispose();
    }



    /// <summary>
    /// Loads the key, creating it when the installation has none yet. True when it was created now: the
    /// backfill check then signs the rows written before signing existed.
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
            var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);   // resolved late: the ring is a host concern
            var row = await context.Set<IntegrityKey>().OrderBy(k => k.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (row is not null)
            {
                _key = Convert.FromBase64String(protector.Unprotect(ProtectedText.Unwrap(row.ProtectedKey)));
                return false;
            }

            var key = RandomNumberGenerator.GetBytes(32);
            context.Set<IntegrityKey>().Add(new IntegrityKey { ProtectedKey = ProtectedText.Wrap(protector.Protect(Convert.ToBase64String(key))), CreatedAt = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow() });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            LogKeyCreated(_logger);
            _key = key;
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }



    private async Task<byte[]> KeyAsync(CancellationToken cancellationToken)
    {
        await EnsureKeyAsync(cancellationToken).ConfigureAwait(false);
        return _key!;
    }



    [LoggerMessage(Level = LogLevel.Error, Message = "Integrity failure: {Table} row {Id} does not match its signature; the row is not honoured. Investigate direct database changes.")]
    private static partial void LogIntegrityFailure(ILogger logger, string table, long id);



    [LoggerMessage(Level = LogLevel.Information, Message = "Integrity key created in wms.integrity_key; back it up with the database and the key ring.")]
    private static partial void LogKeyCreated(ILogger logger);
}
