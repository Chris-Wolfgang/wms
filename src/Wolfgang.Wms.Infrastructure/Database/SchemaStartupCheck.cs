// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// Refuses to start on a schema that is behind (E4.4) or ahead (E4.6) of this build, naming the migrations.
/// The API never applies a migration itself: its service account has no schema rights, so <c>wms-migrate</c>
/// runs as a separate step with the DBA's. An unreachable database also stops startup: the API cannot serve
/// without it and a health check would fail. Registered whatever the provider: with <c>None</c> it logs that
/// no database is configured and does nothing, and when the provider was <c>None</c> at registration but is
/// something else when the host starts (changed by a later <c>Configure</c>/<c>PostConfigure</c>), no context
/// exists to check and startup fails naming that, rather than running without a database.
/// </summary>
public sealed partial class SchemaStartupCheck : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<DatabaseOptions> _options;
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
        _options = options;
        _logger = logger;
    }



    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The database is unreachable, behind this build, or ahead of it;
    /// or the provider is no longer <c>None</c> but no database was registered.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;   // the validated options; ValidateOnStart has already run
        if (options.ParsedProvider is DatabaseProvider.None)
        {
            LogNoDatabase(_logger);
            return;
        }

        using var scope = _scopes.CreateScope();
        var runner = scope.ServiceProvider.GetService<MigrationRunner>()
            ?? throw new InvalidOperationException($"{DatabaseOptions.SectionName}:Provider is {options.Provider} but no database was registered: the provider was None (or unset) when AddWmsDatabase ran and was changed afterwards. Set it in configuration before the host is built.");
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
    private static partial void LogUpToDate(ILogger logger, string? migration);



    [LoggerMessage(Level = LogLevel.Information, Message = "No database is configured (Wms:Database:Provider is None); the schema endpoint reports not installed.")]
    private static partial void LogNoDatabase(ILogger logger);}
