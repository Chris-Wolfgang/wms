// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// Keeps <see cref="LicenseState"/> in step with the installed keys (E79.11): a refresh once the host has
/// started (after the schema check and the seeders) and every <see cref="Interval"/> after, so a key
/// installed on one instance is in force on every instance within seconds and no restart is needed. A
/// failing refresh is logged and retried on the next tick; the license in force is unchanged meanwhile.
/// </summary>
public sealed partial class LicenseSync : BackgroundService
{
    /// <summary>
    /// How often the keys are re-read.
    /// </summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(5);



    private readonly LicenseState _state;
    private readonly TimeProvider _timeProvider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<LicenseSync> _logger;



    /// <summary>
    /// Creates the sync.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public LicenseSync(LicenseState state, TimeProvider timeProvider, IHostApplicationLifetime lifetime, ILogger<LicenseSync> logger)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await StartedAsync(stoppingToken).ConfigureAwait(false);
            await TryRefreshAsync(stoppingToken).ConfigureAwait(false);
            using var timer = new PeriodicTimer(Interval, _timeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await TryRefreshAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // stopping
        }
    }



    private Task StartedAsync(CancellationToken cancellationToken)
    {
        if (_lifetime.ApplicationStarted.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        cancellationToken.Register(() => started.TrySetCanceled(cancellationToken));
        return started.Task;
    }



    private async Task TryRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _state.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogRefreshFailed(_logger, exception);
        }
    }



    [LoggerMessage(Level = LogLevel.Error, Message = "Reading the installed license keys failed; the license in force is unchanged until the next try.")]
    private static partial void LogRefreshFailed(ILogger logger, Exception exception);
}
