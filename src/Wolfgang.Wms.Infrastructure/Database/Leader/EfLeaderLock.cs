// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Jobs;

namespace Wolfgang.Wms.Infrastructure.Database.Leader;

/// <summary>
/// <see cref="ILeaderLock"/> over <c>wms.leader_lock</c> (E12.6): one conditional UPDATE takes or renews a
/// lock (the row is the holder's, or its lease has ended); the first taker inserts the row. Every instance
/// has its own holder id (machine name and a random suffix); a lease renews every third of its length on a
/// background loop and reports itself lost when a renewal does not stick.
/// </summary>
public sealed partial class EfLeaderLock : ILeaderLock
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EfLeaderLock> _logger;



    /// <summary>
    /// Creates the lock.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfLeaderLock(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<EfLeaderLock> logger)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Holder = Environment.MachineName + ":" + Guid.NewGuid().ToString("N")[..12];
    }



    /// <summary>
    /// This instance's holder identifier.
    /// </summary>
    public string Holder { get; }



    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank or too long.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lease"/> is not positive.</exception>
    public async Task<ILeaderLease?> TryAcquireAsync(string name, TimeSpan lease, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, LeaderLock.NameLength);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lease, TimeSpan.Zero);

        if (!await TakeOrRenewAsync(name, lease, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        LogAcquired(_logger, name, Holder);
        return new Lease(this, name, lease);
    }



    /// <summary>
    /// Takes the lock when free, expired or already this instance's; true when it is held afterwards.
    /// </summary>
    public async Task<bool> TakeOrRenewAsync(string name, TimeSpan lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var now = _timeProvider.GetUtcNow();
        var until = now + lease;
        var holder = Holder;
        var updated = await context.Set<LeaderLock>()
            .Where(l => l.Name == name && (l.Holder == holder || l.ExpiresAt <= now))
            .ExecuteUpdateAsync(set => set.SetProperty(l => l.Holder, holder).SetProperty(l => l.AcquiredAt, l => l.Holder == holder ? l.AcquiredAt : now).SetProperty(l => l.ExpiresAt, until), cancellationToken)
            .ConfigureAwait(false);
        if (updated == 1)
        {
            return true;
        }

        if (await context.Set<LeaderLock>().AnyAsync(l => l.Name == name, cancellationToken).ConfigureAwait(false))
        {
            return false;   // another live holder
        }

        context.Set<LeaderLock>().Add(new LeaderLock { Name = name, Holder = holder, AcquiredAt = now, ExpiresAt = until });
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;   // another instance inserted first; it holds the lock
        }
    }



    /// <summary>
    /// Releases the lock when this instance holds it.
    /// </summary>
    public async Task ReleaseAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var now = _timeProvider.GetUtcNow();
        var holder = Holder;
        await context.Set<LeaderLock>()
            .Where(l => l.Name == name && l.Holder == holder)
            .ExecuteUpdateAsync(set => set.SetProperty(l => l.Holder, string.Empty).SetProperty(l => l.ExpiresAt, now), cancellationToken)
            .ConfigureAwait(false);
        LogReleased(_logger, name, holder);
    }



    [LoggerMessage(Level = LogLevel.Information, Message = "Leader lock '{Name}' acquired by {Holder}.")]
    private static partial void LogAcquired(ILogger logger, string name, string holder);



    [LoggerMessage(Level = LogLevel.Information, Message = "Leader lock '{Name}' released by {Holder}.")]
    private static partial void LogReleased(ILogger logger, string name, string holder);



    [LoggerMessage(Level = LogLevel.Warning, Message = "Leader lock '{Name}' lost by {Holder}: the lease could not be renewed.")]
    private static partial void LogLost(ILogger logger, string name, string holder);



    [LoggerMessage(Level = LogLevel.Error, Message = "Leader lock '{Name}': renewal failed; retrying until the lease ends.")]
    private static partial void LogRenewFailed(ILogger logger, string name, Exception exception);



    private sealed class Lease : ILeaderLease
    {
        private readonly EfLeaderLock _owner;
        private readonly TimeSpan _length;
        private readonly CancellationTokenSource _lost = new();
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _renewing;
        private bool _disposed;

        public Lease(EfLeaderLock owner, string name, TimeSpan length)
        {
            _owner = owner;
            Name = name;
            _length = length;
            Lost = _lost.Token;
            _renewing = RenewLoopAsync();
        }

        public string Name { get; }

        public CancellationToken Lost { get; }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;   // releasing twice is harmless
            }

            _disposed = true;
            await _stop.CancelAsync().ConfigureAwait(false);
            try
            {
                await _renewing.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // stopped
            }

            if (!_lost.IsCancellationRequested)
            {
                await _owner.ReleaseAsync(Name, CancellationToken.None).ConfigureAwait(false);
            }

            _lost.Dispose();
            _stop.Dispose();
        }

        private async Task RenewLoopAsync()
        {
            var interval = TimeSpan.FromTicks(Math.Max(_length.Ticks / 3, TimeSpan.TicksPerMillisecond * 100));
            using var timer = new PeriodicTimer(interval, _owner._timeProvider);
            while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false))
            {
                try
                {
                    if (!await _owner.TakeOrRenewAsync(Name, _length, _stop.Token).ConfigureAwait(false))
                    {
                        LogLost(_owner._logger, Name, _owner.Holder);
                        await _lost.CancelAsync().ConfigureAwait(false);
                        return;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogRenewFailed(_owner._logger, Name, exception);   // the next tick tries again; the lease itself decides when it is lost
                }
            }
        }
    }
}
