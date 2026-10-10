// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Identity.BreakGlass;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// <see cref="ILocalLoginGate"/> over <c>core.local_login_gate</c> (E9.3). The row is created on the first
/// write; every change is audited (E6.4) with the actor the caller names (the host OS user for an unlock or
/// lock, the provider for the verification) and logged at Warning, because each one changes who can sign in.
/// </summary>
public sealed partial class EfLocalLoginGate : ILocalLoginGate
{
    private readonly WmsDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EfLocalLoginGate> _logger;



    /// <summary>
    /// Creates the gate.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfLocalLoginGate(WmsDbContext context, TimeProvider timeProvider, ILogger<EfLocalLoginGate> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <inheritdoc/>
    public async Task<LocalLoginGateInfo> GetAsync(CancellationToken cancellationToken)
    {
        var row = await _context.LocalLoginGates.AsNoTracking().OrderBy(g => g.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row?.ToInfo() ?? LocalLoginGateInfo.Open;
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="provider"/> is blank.</exception>
    public async Task<LocalLoginGateInfo> MarkSsoVerifiedAsync(string provider, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        var row = await RowAsync(cancellationToken).ConfigureAwait(false);
        if (row.SsoVerifiedAt is null)
        {
            row.SsoVerifiedAt = _timeProvider.GetUtcNow();
            row.SsoVerifiedProvider = provider;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            LogSsoVerified(_logger, provider);
        }

        return row.ToInfo();
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
        var row = await RowAsync(cancellationToken).ConfigureAwait(false);
        row.UnlockedUntil = now + window;
        row.UnlockedAt = now;
        row.UnlockedBy = openedBy;
        row.LockedAt = null;
        row.LockedBy = null;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogUnlocked(_logger, openedBy, row.UnlockedUntil.Value);
        return row.ToInfo();
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="closedBy"/> is blank.</exception>
    public async Task<LocalLoginGateInfo> LockAsync(string closedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(closedBy);

        var now = _timeProvider.GetUtcNow();
        var row = await RowAsync(cancellationToken).ConfigureAwait(false);
        if (row.ToInfo().IsUnlockedAt(now))
        {
            row.UnlockedUntil = now;
            row.LockedAt = now;
            row.LockedBy = closedBy;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            LogLocked(_logger, closedBy);
        }

        return row.ToInfo();
    }



    /// <summary>
    /// The tracked singleton row, added (not yet saved) when the table is empty.
    /// </summary>
    private async Task<LocalLoginGate> RowAsync(CancellationToken cancellationToken)
    {
        var row = await _context.LocalLoginGates.OrderBy(g => g.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            row = new LocalLoginGate();
            await _context.LocalLoginGates.AddAsync(row, cancellationToken).ConfigureAwait(false);
        }

        return row;
    }



    [LoggerMessage(Level = LogLevel.Warning, Message = "Single sign-on verified through '{Provider}': local sign-in is closed from now on except inside an unlock window opened from the host.")]
    private static partial void LogSsoVerified(ILogger logger, string provider);



    [LoggerMessage(Level = LogLevel.Warning, Message = "Local sign-in unlocked from the host by '{OpenedBy}' until {UnlockedUntil:O}.")]
    private static partial void LogUnlocked(ILogger logger, string openedBy, DateTimeOffset unlockedUntil);



    [LoggerMessage(Level = LogLevel.Warning, Message = "Local sign-in unlock window closed early from the host by '{ClosedBy}'.")]
    private static partial void LogLocked(ILogger logger, string closedBy);
}
