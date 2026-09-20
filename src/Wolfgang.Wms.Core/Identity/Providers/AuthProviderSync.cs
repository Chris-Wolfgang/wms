// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// Keeps <see cref="AuthProviderState"/> in step with the settings (E11.0): a refresh as soon as the host
/// has started (after the schema check and the seeders) and every <see cref="Interval"/> thereafter (the
/// settings cache re-reads the table on the same cadence, so a change made on any instance shows on every
/// instance within two intervals). A failing refresh is logged and retried on the next tick.
/// </summary>
public sealed partial class AuthProviderSync : BackgroundService
{
    /// <summary>
    /// How often the setting is re-read.
    /// </summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(5);



    private readonly AuthProviderState _state;
    private readonly TimeProvider _timeProvider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<AuthProviderSync> _logger;



    /// <summary>
    /// Creates the sync.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public AuthProviderSync(AuthProviderState state, TimeProvider timeProvider, IHostApplicationLifetime lifetime, ILogger<AuthProviderSync> logger)
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
            await StartedAsync(stoppingToken).ConfigureAwait(false);   // after every other hosted service: the schema is migrated, the seeders ran
            await RefreshAsync(stoppingToken).ConfigureAwait(false);
            using var timer = new PeriodicTimer(Interval, _timeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await RefreshAsync(stoppingToken).ConfigureAwait(false);
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



    private async Task RefreshAsync(CancellationToken cancellationToken)
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



    [LoggerMessage(Level = LogLevel.Error, Message = "Reading auth.providers.enabled failed; the enabled providers are unchanged until the next try.")]
    private static partial void LogRefreshFailed(ILogger logger, Exception exception);
}
