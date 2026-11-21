// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Logging;

namespace Wolfgang.Wms.Core.Logging;

/// <summary>
/// Moves the host's level switch from the settings (E12.4): once the host has started and every
/// <see cref="Interval"/> after, so a change made through the API (or on another instance) applies within
/// seconds and a timed elevation reverts by itself.
/// </summary>
public sealed partial class LogLevelSync : BackgroundService
{
    /// <summary>
    /// How often the settings are re-read.
    /// </summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(5);



    private readonly WmsLogLevel _level;
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _timeProvider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<LogLevelSync> _logger;



    /// <summary>
    /// Creates the sync.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public LogLevelSync(WmsLogLevel level, IServiceScopeFactory scopes, TimeProvider timeProvider, IHostApplicationLifetime lifetime, ILogger<LogLevelSync> logger)
    {
        _level = level ?? throw new ArgumentNullException(nameof(level));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <summary>
    /// Reads the settings and applies the level in force; true when the switch moved.
    /// </summary>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettings>();
        var organization = SettingScopeRef.Organization;
        var level = await settings.GetAsync(LogLevelSettings.Level, organization, cancellationToken).ConfigureAwait(false);
        var elevated = await settings.GetAsync(LogLevelSettings.ElevatedLevel, organization, cancellationToken).ConfigureAwait(false);
        var until = await settings.GetAsync(LogLevelSettings.ElevatedUntil, organization, cancellationToken).ConfigureAwait(false);
        var effective = LogLevelSettings.Effective(level, elevated, until, _timeProvider.GetUtcNow());
        if (!_level.Apply(WmsLogLevel.ToSerilog(effective)))
        {
            return false;
        }

        LogChanged(_logger, effective, until > _timeProvider.GetUtcNow() ? until : null);
        return true;
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
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogRefreshFailed(_logger, exception);
        }
    }



    [LoggerMessage(Level = LogLevel.Information, Message = "Log level is now {Level} (elevated until {Until}).")]
    private static partial void LogChanged(ILogger logger, LogLevel level, DateTimeOffset? until);



    [LoggerMessage(Level = LogLevel.Error, Message = "Reading the logging settings failed; the level is unchanged until the next try.")]
    private static partial void LogRefreshFailed(ILogger logger, Exception exception);
}
