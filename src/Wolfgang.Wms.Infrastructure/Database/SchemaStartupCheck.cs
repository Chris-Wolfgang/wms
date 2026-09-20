// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// Refuses to start on a schema that is behind (E4.4) or ahead (E4.6) of this build, naming the migrations,
/// unless <see cref="DatabaseOptions.AutoMigrate"/> applies the pending ones first (bundled installs only).
/// An unreachable database also stops startup: the API cannot serve without it and a health check would fail.
/// </summary>
public sealed partial class SchemaStartupCheck : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly DatabaseOptions _options;
    private readonly ILogger<SchemaStartupCheck> _logger;



    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public SchemaStartupCheck(IServiceScopeFactory scopes, IOptions<DatabaseOptions> options, ILogger<SchemaStartupCheck> logger)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }



    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The database is unreachable, behind this build without
    /// <c>AutoMigrate</c>, or ahead of it.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
        var status = await runner.StatusAsync(cancellationToken).ConfigureAwait(false);

        if (!status.Reachable)
        {
            throw new InvalidOperationException($"{DatabaseOptions.SectionName}: the database cannot be reached; check the connection string and that the server is up.");
        }

        if (status.SchemaIsNewer)
        {
            throw new InvalidOperationException($"The database schema is newer than this build (unknown migrations: {string.Join(", ", status.Unknown)}). Upgrade the application, or restore the backup taken before the upgrade.");
        }

        if (status.Pending.Count == 0)
        {
            LogUpToDate(_logger, status.Current);
            return;
        }

        if (!_options.AutoMigrate)
        {
            throw new InvalidOperationException($"The database schema is behind this build; pending migrations: {string.Join(", ", status.Pending)}. Run wms-migrate, or set {DatabaseOptions.SectionName}:AutoMigrate=true for a bundled install.");
        }

        var result = await runner.ApplyAsync(target: null, confirmDataLoss: false, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"AutoMigrate failed at {result.FailedMigration}: {result.Error}");
        }

        LogMigrated(_logger, string.Join(", ", result.Steps));
    }



    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }



    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema is up to date at {Migration}.")]
    private static partial void LogUpToDate(ILogger logger, string? migration);



    [LoggerMessage(Level = LogLevel.Warning, Message = "AutoMigrate applied pending migrations: {Migrations}.")]
    private static partial void LogMigrated(ILogger logger, string migrations);
}
