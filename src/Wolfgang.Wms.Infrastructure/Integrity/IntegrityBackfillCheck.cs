// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;

namespace Wolfgang.Wms.Infrastructure.Integrity;

/// <summary>
/// Creates the integrity key on the first start that finds none and signs every security row as it stands
/// (E10.4: the upgrade path; nothing could have been validly signed without the key). Once the key exists
/// a row that fails verification is never re-signed here: repair goes through the application.
/// </summary>
public sealed partial class IntegrityBackfillCheck : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IntegritySigner _signer;
    private readonly IOptions<KeyRingOptions> _keyRing;
    private readonly ILogger<IntegrityBackfillCheck> _logger;



    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public IntegrityBackfillCheck(IServiceScopeFactory scopes, IntegritySigner signer, IOptions<KeyRingOptions> keyRing, ILogger<IntegrityBackfillCheck> logger)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _keyRing = keyRing ?? throw new ArgumentNullException(nameof(keyRing));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_keyRing.Value.UsesFileSystem)
        {
            LogRingInDatabase(_logger);   // docs/INTEGRITY.md, threat model; #1078 protects the ring itself
        }

        if (!await _signer.EnsureKeyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var users = await context.Users.ToListAsync(cancellationToken).ConfigureAwait(false);
        var roles = await context.Roles.Include(r => r.Permissions).ToListAsync(cancellationToken).ConfigureAwait(false);
        var assignments = await context.UserRoles.ToListAsync(cancellationToken).ConfigureAwait(false);
        var pending = users.Cast<ISignedEntity>().Concat(roles).Concat(assignments).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        await _signer.SignAllAsync(pending, cancellationToken).ConfigureAwait(false);
        foreach (var entity in pending)
        {
            context.Entry(entity).Property(nameof(ISignedEntity.Signature)).IsModified = true;   // an unchanged signature still writes
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogBackfilled(_logger, pending.Count);
    }



    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }



    [LoggerMessage(Level = LogLevel.Warning, Message = "Integrity: the Data Protection key ring is stored in the database (wms.data_protection_key) without its own protection, so anyone who can read the database can recover the integrity key and sign changed rows. Signatures still catch accidental and limited-privilege changes; set Wms:DataProtection:KeyRingPath to a directory only the application can read for the full guarantee.")]
    private static partial void LogRingInDatabase(ILogger logger);



    [LoggerMessage(Level = LogLevel.Warning, Message = "Integrity: key created and {Count} existing security rows signed as they stand (expected once, on the first start after the upgrade; an incident on any other start).")]
    private static partial void LogBackfilled(ILogger logger, int count);
}
