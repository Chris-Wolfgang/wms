// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Migrate;

/// <summary>
/// The <c>wms-migrate</c> tool (E4), testable in-process: parses the command line, merges it over the
/// <c>Wms:Database</c> configuration, and runs status, script or apply through <see cref="MigrationRunner"/>.
/// </summary>
public static class MigrateProgram
{
    /// <summary>Everything ran.</summary>
    public const int ExitOk = 0;

    /// <summary>A migration failed; its name is in the output.</summary>
    public const int ExitMigrationFailed = 1;

    /// <summary>Usage or configuration error.</summary>
    public const int ExitUsage = 2;

    /// <summary>A downgrade needs <c>--confirm-data-loss</c>.</summary>
    public const int ExitConfirmationRequired = 3;

    /// <summary>Nothing was applied (or, with <c>--status</c>, nothing could be): the database cannot be reached, its schema is newer than this build, or its migrations history has a gap.</summary>
    public const int ExitRefused = 4;



    /// <summary><c>--status</c> only: the database is reachable and consistent but lacks shipped migrations (listed in the output).</summary>
    public const int ExitSchemaBehind = 5;



    /// <summary>Cancelled (Ctrl+C) before the run completed; a migration in flight was rolled back by the database.</summary>
    public const int ExitCancelled = 130;



    /// <summary>
    /// Runs the tool.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="output">Standard output.</param>
    /// <param name="error">Standard error.</param>
    /// <param name="configuration">Configuration providing <c>Wms:Database</c>; null reads appsettings.json and the environment.</param>
    /// <param name="cancellationToken">Cancels a running migration.</param>
    /// <returns>The process exit code.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output, TextWriter error, IConfiguration? configuration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var command = MigrateCommandLine.Parse(args);
        if (command.Help)
        {
            await output.WriteLineAsync(MigrateCommandLine.Usage).ConfigureAwait(false);
            return ExitOk;
        }

        if (command.Error is not null)
        {
            await error.WriteLineAsync(command.Error).ConfigureAwait(false);
            await error.WriteLineAsync(MigrateCommandLine.Usage).ConfigureAwait(false);
            return ExitUsage;
        }

        var options = await LoadOptionsAsync(command, configuration, error).ConfigureAwait(false);
        if (options is null)
        {
            return ExitUsage;
        }

        var problems = options.Validate(connectionStringRequired: !command.Script);
        if (options.ParsedProvider is DatabaseProvider.None)
        {
            problems = [.. problems, $"{DatabaseOptions.SectionName}:Provider must be SqlServer or PostgreSql (use --provider)."];
        }

        if (problems.Count > 0)
        {
            foreach (var problem in problems)
            {
                await error.WriteLineAsync(problem).ConfigureAwait(false);
            }

            return ExitUsage;
        }

        // Validate above has already parsed the connection string with the provider's builder, so this cannot throw.
        using var context = CreateContext(options);
        return await RunModeAsync(new MigrationRunner(context), command, output, error, cancellationToken).ConfigureAwait(false);
    }



    /// <summary>
    /// Command-line flags over the configured <c>Wms:Database</c> section.
    /// </summary>
    public static DatabaseOptions Options(MigrateCommandLine command, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new DatabaseOptions();
        configuration.GetSection(DatabaseOptions.SectionName).Bind(options);
        if (command.Provider is not null)
        {
            options.Provider = command.Provider;
        }

        if (command.ConnectionString is not null)
        {
            options.ConnectionString = command.ConnectionString;
        }

        if (command.TrustServerCertificate is not null)
        {
            options.TrustServerCertificate = command.TrustServerCertificate.Value;
        }

        return options;
    }



    private static async Task<int> RunModeAsync(MigrationRunner runner, MigrateCommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        try
        {
            if (command.Status)
            {
                return await GuardAsync(() => StatusAsync(runner, output, cancellationToken), error).ConfigureAwait(false);
            }

            if (command.Script)
            {
                return await ScriptAsync(runner, command, output, error, cancellationToken).ConfigureAwait(false);
            }

            return await ApplyAsync(runner, command, output, error, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return ExitUsage;
        }
    }



    private static WmsDbContext CreateContext(DatabaseOptions options)
    {
        var builder = new DbContextOptionsBuilder<WmsDbContext>();
        DatabaseServiceCollectionExtensions.Configure(builder, options);
        return new WmsDbContext(builder.Options);
    }



    private static async Task<DatabaseOptions?> LoadOptionsAsync(MigrateCommandLine command, IConfiguration? configuration, TextWriter error)
    {
        try
        {
            return Options(command, configuration ?? DefaultConfiguration());
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            // A malformed appsettings.json (InvalidDataException) or a value that does not bind is a configuration
            // error, not a crash.
            await error.WriteLineAsync("Configuration is not valid: " + exception.Message).ConfigureAwait(false);
            return null;
        }
    }



    private static IConfiguration DefaultConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }



    /// <summary>
    /// The <c>--status</c> mode. The exit code says what was found, so an installer or health script can gate on
    /// it without parsing: <see cref="ExitOk"/> up to date, <see cref="ExitSchemaBehind"/> pending migrations,
    /// <see cref="ExitRefused"/> unreachable, newer than this build, or an inconsistent history. When the
    /// database cannot be queried the pending list is unknown and is reported as such rather than as every
    /// shipped migration.
    /// </summary>
    private static async Task<int> StatusAsync(MigrationRunner runner, TextWriter output, CancellationToken cancellationToken)
    {
        var status = await runner.StatusAsync(cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync("Expected: " + (status.Expected ?? "(no migrations shipped)")).ConfigureAwait(false);
        await output.WriteLineAsync("Reachable: " + (status.Reachable ? "yes" : "no: " + status.Error)).ConfigureAwait(false);
        await WriteListAsync(output, "Applied", status.Applied).ConfigureAwait(false);
        if (status.Reachable)
        {
            await WriteListAsync(output, "Pending", status.Pending).ConfigureAwait(false);
        }
        else
        {
            await output.WriteLineAsync($"Pending: unknown (the database cannot be queried; this build ships {status.Pending.Count} migration(s))").ConfigureAwait(false);
        }

        if (status.SchemaIsNewer)
        {
            await WriteListAsync(output, "Unknown to this build", status.Unknown).ConfigureAwait(false);
        }

        await output.WriteLineAsync(status.UpToDate ? "Schema is up to date." : "Schema is not up to date.").ConfigureAwait(false);
        if (MigrationRunner.Refusal(status) is not null)
        {
            return ExitRefused;
        }

        return status.Pending.Count > 0 ? ExitSchemaBehind : ExitOk;
    }



    /// <summary>
    /// Runs a mode so that a cancellation (Ctrl+C) or a failure outside a migration (the provider cannot be
    /// loaded, EF rejects the model, an unexpected I/O error) ends with a documented exit code and a one-line
    /// message instead of a stack trace and the runtime's unhandled-exception code. A usage error
    /// (<see cref="ArgumentException"/>) passes through to the caller's usage handling.
    /// </summary>
    private static async Task<int> GuardAsync(Func<Task<int>> mode, TextWriter error)
    {
        try
        {
            return await mode().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Cancelled.").ConfigureAwait(false);
            return ExitCancelled;
        }
        catch (Exception exception) when (exception is not ArgumentException)
        {
            await error.WriteLineAsync("wms-migrate failed: " + exception.Message).ConfigureAwait(false);
            return ExitMigrationFailed;
        }
    }



    /// <summary>
    /// The <c>--script</c> mode over <paramref name="runner"/>: writes the script to <paramref name="output"/> or
    /// <see cref="MigrateCommandLine.Output"/>, or, for a data-losing downgrade without
    /// <see cref="MigrateCommandLine.ConfirmDataLoss"/>, lists the steps on <paramref name="error"/> and writes
    /// nothing (E4.6).
    /// </summary>
    /// <returns><see cref="ExitOk"/>, <see cref="ExitConfirmationRequired"/>, <see cref="ExitUsage"/> when
    /// <see cref="MigrateCommandLine.Output"/> cannot be written, <see cref="ExitCancelled"/> or
    /// <see cref="ExitMigrationFailed"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">A target names no shipped migration, or more than one.</exception>
    public static Task<int> ScriptAsync(MigrationRunner runner, MigrateCommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        return GuardAsync(() => ScriptCoreAsync(runner, command, output, error, cancellationToken), error);
    }



    private static async Task<int> ScriptCoreAsync(MigrationRunner runner, MigrateCommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var script = runner.Script(command.From, command.To, command.ConfirmDataLoss);
        if (script.Sql is null)
        {
            await WriteConfirmationRequiredAsync(error, "This downgrade script loses data", script.DestructiveSteps).ConfigureAwait(false);
            return ExitConfirmationRequired;
        }

        if (command.Output is null)
        {
            await output.WriteAsync(script.Sql).ConfigureAwait(false);
            return ExitOk;
        }

        try
        {
            await File.WriteAllTextAsync(command.Output, script.Sql, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await error.WriteLineAsync($"Cannot write --output {command.Output}: {exception.Message}").ConfigureAwait(false);
            return ExitUsage;
        }

        await output.WriteLineAsync("Wrote " + command.Output).ConfigureAwait(false);
        return ExitOk;
    }



    /// <summary>
    /// The apply mode (no <c>--status</c> or <c>--script</c>) over <paramref name="runner"/>: moves the schema
    /// to <see cref="MigrateCommandLine.To"/> and reports the direction and the migrations it ran.
    /// </summary>
    /// <returns><see cref="ExitOk"/>, <see cref="ExitMigrationFailed"/>, <see cref="ExitConfirmationRequired"/>,
    /// <see cref="ExitRefused"/> or <see cref="ExitCancelled"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The target names no shipped migration, or more than one.</exception>
    public static Task<int> ApplyAsync(MigrationRunner runner, MigrateCommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        return GuardAsync(() => ApplyCoreAsync(runner, command, output, error, cancellationToken), error);
    }



    private static async Task<int> ApplyCoreAsync(MigrationRunner runner, MigrateCommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var result = await runner.ApplyAsync(command.To, command.ConfirmDataLoss, cancellationToken).ConfigureAwait(false);
        if (result.Refused)
        {
            await error.WriteLineAsync("Nothing was applied. " + result.Error).ConfigureAwait(false);
            return ExitRefused;
        }

        if (result.RequiresConfirmation)
        {
            await WriteConfirmationRequiredAsync(error, "This downgrade loses data", result.DestructiveSteps).ConfigureAwait(false);
            return ExitConfirmationRequired;
        }

        await output.WriteLineAsync($"Direction: {result.Direction} (from {result.From ?? MigrationRunner.Empty} to {result.To ?? MigrationRunner.Empty})").ConfigureAwait(false);
        await WriteListAsync(output, result.Direction == MigrationDirection.Down ? "Reverted" : "Applied", result.Steps).ConfigureAwait(false);
        if (result.FailedMigration is not null)
        {
            await error.WriteLineAsync($"Migration {result.FailedMigration} failed: {result.Error}").ConfigureAwait(false);
            return ExitMigrationFailed;
        }

        await output.WriteLineAsync(result.Direction == MigrationDirection.None ? "Nothing to do." : "Done.").ConfigureAwait(false);
        return ExitOk;
    }



    private static async Task WriteConfirmationRequiredAsync(TextWriter error, string what, IReadOnlyList<string> steps)
    {
        await error.WriteLineAsync(what + "; re-run with --confirm-data-loss to proceed:").ConfigureAwait(false);
        foreach (var step in steps)
        {
            await error.WriteLineAsync("  " + step).ConfigureAwait(false);
        }
    }



    private static async Task WriteListAsync(TextWriter output, string title, IReadOnlyList<string> items)
    {
        await output.WriteLineAsync($"{title} ({items.Count}):").ConfigureAwait(false);
        foreach (var item in items)
        {
            await output.WriteLineAsync("  " + item).ConfigureAwait(false);
        }
    }
}
