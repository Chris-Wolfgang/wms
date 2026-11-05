// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Identity.BreakGlass;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Integrity;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// <see cref="ILocalLoginGate"/> over <c>core.local_login_gate</c> (E9.3). The row is created on the first
/// write; every change is audited (E6.4) with the actor the caller names (the host OS user for an unlock or
/// lock, the provider for the verification) and logged at Warning, because each one changes who can sign in.
/// The row is signed (E10.4): one that fails verification is logged at Error and read as closed (SSO verified,
/// no window), and the next write repairs it in that state, so a database edit can never open local sign-in
/// while the host's unlock still works. The first write is one transaction, insert and signature together,
/// and the loser of a race on it retries against the winner's row.
/// </summary>
public sealed partial class EfLocalLoginGate : ILocalLoginGate
{
    /// <summary>The provider recorded when a row that failed verification is repaired without one.</summary>
    internal const string IntegrityRepairProvider = "integrity-repair";

    private readonly WmsDbContext _context;
    private readonly IIntegritySigner _signer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EfLocalLoginGate> _logger;



    /// <summary>
    /// Creates the gate.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfLocalLoginGate(WmsDbContext context, IIntegritySigner signer, TimeProvider timeProvider, ILogger<EfLocalLoginGate> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <inheritdoc/>
    public async Task<LocalLoginGateInfo> GetAsync(CancellationToken cancellationToken)
    {
        var row = await _context.LocalLoginGates.AsNoTracking().OrderBy(g => g.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return LocalLoginGateInfo.Open;
        }

        if (await IsValidAsync(row, cancellationToken).ConfigureAwait(false))
        {
            return row.ToInfo();
        }

        FailClosed(row);
        return row.ToInfo();
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="provider"/> is blank.</exception>
    public async Task<LocalLoginGateInfo> MarkSsoVerifiedAsync(string provider, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        var marked = false;
        var info = await ChangeAsync
        (
            row =>
            {
                marked = row.SsoVerifiedAt is null;
                if (marked)
                {
                    row.SsoVerifiedAt = _timeProvider.GetUtcNow();
                    row.SsoVerifiedProvider = provider;
                }

                return marked;
            },
            cancellationToken
        ).ConfigureAwait(false);
        if (marked)
        {
            LogSsoVerified(_logger, provider);
        }

        return info;
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="openedBy"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="window"/> is outside <see cref="LocalLoginGateRules"/>.</exception>
    public async Task<LocalLoginGateInfo> UnlockAsync(TimeSpan window, string openedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(openedBy);
        if (LocalLoginGateRules.Validate(window) is { } reason)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, reason);
        }

        var now = _timeProvider.GetUtcNow();
        var info = await ChangeAsync
        (
            row =>
            {
                row.UnlockedUntil = now + window;
                row.UnlockedAt = now;
                row.UnlockedBy = openedBy;
                row.LockedAt = null;
                row.LockedBy = null;
                return true;
            },
            cancellationToken
        ).ConfigureAwait(false);
        LogUnlocked(_logger, openedBy, now + window);
        return info;
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="closedBy"/> is blank.</exception>
    public async Task<LocalLoginGateInfo> LockAsync(string closedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(closedBy);

        var now = _timeProvider.GetUtcNow();
        var locked = false;
        var info = await ChangeAsync
        (
            row =>
            {
                locked = row.ToInfo().IsUnlockedAt(now);
                if (locked)
                {
                    row.UnlockedUntil = now;
                    row.LockedAt = now;
                    row.LockedBy = closedBy;
                }

                return locked;
            },
            cancellationToken
        ).ConfigureAwait(false);
        if (locked)
        {
            LogLocked(_logger, closedBy);
        }

        return info;
    }



    /// <summary>
    /// Applies <paramref name="change"/> (true when it changed the row) to the singleton and saves. With no row
    /// yet, one is inserted only if the change has something to record; a race on that insert is lost at most
    /// once, because the winner's row then exists.
    /// </summary>
    private async Task<LocalLoginGateInfo> ChangeAsync(Func<LocalLoginGate, bool> change, CancellationToken cancellationToken)
    {
        var row = await _context.LocalLoginGates.OrderBy(g => g.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            var candidate = new LocalLoginGate();
            if (!change(candidate))
            {
                return LocalLoginGateInfo.Open;
            }

            if (await TryInsertAsync(candidate, cancellationToken).ConfigureAwait(false))
            {
                return candidate.ToInfo();
            }

            row = await _context.LocalLoginGates.OrderBy(g => g.Id).FirstAsync(cancellationToken).ConfigureAwait(false);
        }

        var repaired = !await IsValidAsync(row, cancellationToken).ConfigureAwait(false);
        if (repaired)
        {
            FailClosed(row);
        }

        if (change(row) || repaired)
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return row.ToInfo();
    }



    /// <summary>
    /// Inserts and signs the first row in one transaction, so no reader sees it unsigned; false when another
    /// writer's row got there first (the unique index refused this one).
    /// </summary>
    private async Task<bool> TryInsertAsync(LocalLoginGate candidate, CancellationToken cancellationToken)
    {
        try
        {
            await _context.Database
                .CreateExecutionStrategy()
                .ExecuteAsync
                (
                    async token =>
                    {
                        // Each attempt adds the row afresh: a save accepts it before the commit, so a retry over the same entry would write nothing.
                        _context.Entry(candidate).State = EntityState.Detached;
                        candidate.Id = 0;
                        await _context.LocalLoginGates.AddAsync(candidate, token).ConfigureAwait(false);
                        var transaction = await _context.Database.BeginTransactionAsync(token).ConfigureAwait(false);
                        await using (transaction.ConfigureAwait(false))
                        {
                            await _context.SaveChangesAsync(token).ConfigureAwait(false);
                            await transaction.CommitAsync(token).ConfigureAwait(false);
                        }
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException exception)
        {
            LogLostFirstWrite(_logger, exception);
            _context.Entry(candidate).State = EntityState.Detached;
            return false;
        }
    }



    private async Task<bool> IsValidAsync(LocalLoginGate row, CancellationToken cancellationToken)
    {
        if (await _signer.IsValidAsync(row, LocalLoginGateConfiguration.Schema + "." + LocalLoginGateConfiguration.Table, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        LogFailedVerification(_logger);
        return false;
    }



    /// <summary>
    /// The state a row that failed verification is treated as: SSO verified and no window.
    /// </summary>
    private void FailClosed(LocalLoginGate row)
    {
        row.SsoVerifiedAt ??= _timeProvider.GetUtcNow();
        row.SsoVerifiedProvider ??= IntegrityRepairProvider;
        row.UnlockedUntil = null;
        row.UnlockedAt = null;
        row.UnlockedBy = null;
    }



    [LoggerMessage(Level = LogLevel.Error, Message = "core.local_login_gate failed its integrity check: it was changed outside the application. Local sign-in is treated as closed; the next write from the host or a sign-in repairs the row.")]
    private static partial void LogFailedVerification(ILogger logger);



    [LoggerMessage(Level = LogLevel.Information, Message = "Another writer created the local sign-in gate first; retrying against its row.")]
    private static partial void LogLostFirstWrite(ILogger logger, Exception exception);



    [LoggerMessage(Level = LogLevel.Warning, Message = "Single sign-on verified through '{Provider}': local sign-in is closed from now on except inside an unlock window opened from the host.")]
    private static partial void LogSsoVerified(ILogger logger, string provider);



    [LoggerMessage(Level = LogLevel.Warning, Message = "Local sign-in unlocked from the host by '{OpenedBy}' until {UnlockedUntil:O}.")]
    private static partial void LogUnlocked(ILogger logger, string openedBy, DateTimeOffset unlockedUntil);



    [LoggerMessage(Level = LogLevel.Warning, Message = "Local sign-in unlock window closed early from the host by '{ClosedBy}'.")]
    private static partial void LogLocked(ILogger logger, string closedBy);
}
