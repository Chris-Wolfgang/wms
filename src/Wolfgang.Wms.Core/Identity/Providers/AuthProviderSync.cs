// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// Keeps <see cref="AuthProviderState"/> in step with the settings (E11.0): a refresh at startup and every
/// <see cref="Interval"/> thereafter (the settings cache re-reads the table on the same cadence, so a change
/// made on any instance shows on every instance within two intervals). A failing refresh is logged and
/// retried on the next tick.
/// </summary>
public sealed partial class AuthProviderSync : BackgroundService
{
    /// <summary>
    /// How often the setting is re-read.
    /// </summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(5);



    private readonly AuthProviderState _state;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthProviderSync> _logger;



    /// <summary>
    /// Creates the sync.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public AuthProviderSync(AuthProviderState state, TimeProvider timeProvider, ILogger<AuthProviderSync> logger)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <inheritdoc/>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken).ConfigureAwait(false);   // the first request already sees the setting
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }



    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, _timeProvider);
        try
        {
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
