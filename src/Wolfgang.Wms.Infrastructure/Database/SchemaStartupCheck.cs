// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// Refuses to start on a schema that is behind (E4.4) or ahead (E4.6) of this build, naming the migrations.
/// The API never applies a migration itself: its service account has no schema rights, so <c>wms-migrate</c>
/// runs as a separate step with the DBA's. An unreachable database also stops startup: the API cannot serve
/// without it and a health check would fail.
/// </summary>
public sealed partial class SchemaStartupCheck : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SchemaStartupCheck> _logger;



    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public SchemaStartupCheck(IServiceScopeFactory scopes, ILogger<SchemaStartupCheck> logger)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(logger);

        _scopes = scopes;
        _logger = logger;
    }



    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The database is unreachable, behind this build, or ahead of it.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
        var status = await runner.StatusAsync(cancellationToken).ConfigureAwait(false);

        var refusal = MigrationRunner.Refusal(status);
        if (refusal is not null)
        {
            throw new InvalidOperationException(refusal);
        }

        if (status.Pending.Count != 0)
        {
            throw new InvalidOperationException($"The database schema is behind this build; pending migrations: {string.Join(", ", status.Pending)}. Run wms-migrate with the DBA's rights, then start the API.");
        }

        LogUpToDate(_logger, status.Current);
    }



    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }



    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema is up to date at {Migration}.")]
    private static partial void LogUpToDate(ILogger logger, string? migration);}
