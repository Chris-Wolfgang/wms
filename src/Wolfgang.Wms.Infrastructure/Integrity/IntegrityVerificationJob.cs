// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Jobs;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Identity;

namespace Wolfgang.Wms.Infrastructure.Integrity;

/// <summary>
/// Verifies every signed row on a schedule (E10.4): users, roles, assignments and group mappings. A row that fails is
/// logged at Error by the signer and counted here; the summary is logged at Warning when anything failed,
/// Information otherwise. The interval is the <c>auth.integrity.verify_interval</c> setting; the worker
/// runs it (one runner per installation once E12.6's leader lock lands).
/// </summary>
public sealed partial class IntegrityVerificationJob : BackgroundService
{
    /// <summary>
    /// The leader lock name: one instance verifies at a time (E12.6).
    /// </summary>
    public const string LockName = "integrity.verify";



    private readonly IServiceScopeFactory _scopes;
    private readonly ILeaderLock _lock;
    private readonly ILogger<IntegrityVerificationJob> _logger;



    /// <summary>
    /// Creates the job.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public IntegrityVerificationJob(IServiceScopeFactory scopes, ILeaderLock leaderLock, ILogger<IntegrityVerificationJob> logger)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _lock = leaderLock ?? throw new ArgumentNullException(nameof(leaderLock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <summary>
    /// The last run's result, for the health endpoint (E12.1) and tests.
    /// </summary>
    public IntegrityVerificationResult? LastResult { get; private set; }



    /// <summary>
    /// Verifies every signed row once.
    /// </summary>
    public async Task<IntegrityVerificationResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var signer = scope.ServiceProvider.GetRequiredService<IIntegritySigner>();
        var checkedRows = 0;
        var failed = 0;

        foreach (var user in await context.Users.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            checkedRows++;
            failed += await signer.IsValidAsync(user, "core.user", cancellationToken).ConfigureAwait(false) ? 0 : 1;
        }

        foreach (var role in await context.Roles.Include(r => r.Permissions).AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            checkedRows++;
            failed += await signer.IsValidAsync(role, "core.role", cancellationToken).ConfigureAwait(false) ? 0 : 1;
        }

        foreach (var assignment in await context.UserRoles.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            checkedRows++;
            failed += await signer.IsValidAsync(assignment, "core.user_role", cancellationToken).ConfigureAwait(false) ? 0 : 1;
        }

        foreach (var mapping in await context.GroupRoleMappings.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            checkedRows++;
            failed += await signer.IsValidAsync(mapping, "core.group_role_mapping", cancellationToken).ConfigureAwait(false) ? 0 : 1;
        }

        var result = new IntegrityVerificationResult(scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(), checkedRows, failed);
        LastResult = result;
        if (failed > 0)
        {
            LogFailures(_logger, failed, checkedRows);
        }
        else
        {
            LogClean(_logger, checkedRows);
        }

        return result;
    }



    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan interval;
            try
            {
                var lease = await _lock.TryAcquireAsync(LockName, TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
                if (lease is null)
                {
                    LogNotLeader(_logger);   // another instance verifies; this one checks again next interval
                }
                else
                {
                    await using (lease.ConfigureAwait(false))
                    {
                        await RunOnceAsync(stoppingToken).ConfigureAwait(false);
                    }
                }

                interval = await IntervalAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogRunFailed(_logger, exception);
                interval = Wolfgang.Wms.Core.Identity.AuthSettings.IntegrityVerifyInterval.DefaultValue;
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }



    private async Task<TimeSpan> IntervalAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISettings>().GetAsync(Wolfgang.Wms.Core.Identity.AuthSettings.IntegrityVerifyInterval, SettingScopeRef.Organization, cancellationToken).ConfigureAwait(false);
    }



    [LoggerMessage(Level = LogLevel.Warning, Message = "Integrity verification: {Failed} of {CheckedRows} signed rows failed; see the Error entries above.")]
    private static partial void LogFailures(ILogger logger, int failed, int checkedRows);



    [LoggerMessage(Level = LogLevel.Information, Message = "Integrity verification: {CheckedRows} signed rows verified.")]
    private static partial void LogClean(ILogger logger, int checkedRows);



    [LoggerMessage(Level = LogLevel.Error, Message = "Integrity verification run failed; retrying after the default interval.")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);



    [LoggerMessage(Level = LogLevel.Debug, Message = "Integrity verification: another instance holds the leader lock; skipping this interval.")]
    private static partial void LogNotLeader(ILogger logger);
}
