// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Wolfgang.Wms.Core.Secrets;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;

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

        configuration ??= DefaultConfiguration();
        var options = Options(command, configuration);
        if (command.Protect)
        {
            return await ProtectAsync(command, configuration, options, output, error).ConfigureAwait(false);
        }

        var problems = options.Validate();
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

        return await ExecuteAsync(command, configuration, options, output, error, cancellationToken).ConfigureAwait(false);
    }



    /// <summary>
    /// Opens the context (decrypting an <c>enc:v1:</c> connection string with the key ring, E8.2) and runs
    /// the chosen mode.
    /// </summary>
    private static async Task<int> ExecuteAsync(MigrateCommandLine command, IConfiguration configuration, DatabaseOptions options, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var builder = new DbContextOptionsBuilder<WmsDbContext>();
        try
        {
            DatabaseServiceCollectionExtensions.Configure(builder, options, options.ConnectionStringIsProtected ? Protector(command, configuration) : null);
        }
        catch (InvalidOperationException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return ExitUsage;
        }

        using var context = new WmsDbContext(builder.Options);
        var runner = new MigrationRunner(context);
        try
        {
            if (command.Status)
            {
                return await StatusAsync(runner, output, cancellationToken).ConfigureAwait(false);
            }

            if (command.Script)
            {
                return await ScriptAsync(runner, command, output, cancellationToken).ConfigureAwait(false);
            }

            return await ApplyAsync(runner, command, output, error, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return ExitUsage;
        }
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



    /// <summary>
    /// <c>--protect</c> (E8.2): prints the connection string encrypted with the key ring, for the installer to
    /// put in appsettings or an environment variable.
    /// </summary>
    private static async Task<int> ProtectAsync(MigrateCommandLine command, IConfiguration configuration, DatabaseOptions options, TextWriter output, TextWriter error)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            await error.WriteLineAsync("--protect needs a connection string (--connection-string or Wms:Database:ConnectionString).").ConfigureAwait(false);
            return ExitUsage;
        }

        if (options.ConnectionStringIsProtected)
        {
            await error.WriteLineAsync("The connection string is already encrypted (enc:v1:).").ConfigureAwait(false);
            return ExitUsage;
        }

        var protector = Protector(command, configuration);
        if (protector is null)
        {
            await error.WriteLineAsync($"--protect needs the key ring: --key-ring <path> or {KeyRingOptions.PathKey}.").ConfigureAwait(false);
            return ExitUsage;
        }

        await output.WriteLineAsync(protector.Protect(options.ConnectionString)).ConfigureAwait(false);
        return ExitOk;
    }



    private static ISecretProtector? Protector(MigrateCommandLine command, IConfiguration configuration)
    {
        return command.KeyRing is not null ? KeyRing.CreateProtector(command.KeyRing) : KeyRing.TryCreateProtector(configuration);
    }



    private static IConfiguration DefaultConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }



    private static async Task<int> StatusAsync(MigrationRunner runner, TextWriter output, CancellationToken cancellationToken)
    {
        var status = await runner.StatusAsync(cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync("Expected: " + (status.Expected ?? "(no migrations shipped)")).ConfigureAwait(false);
        await output.WriteLineAsync("Reachable: " + (status.Reachable ? "yes" : "no")).ConfigureAwait(false);
        await WriteListAsync(output, "Applied", status.Applied).ConfigureAwait(false);
        await WriteListAsync(output, "Pending", status.Pending).ConfigureAwait(false);
        if (status.SchemaIsNewer)
        {
            await WriteListAsync(output, "Unknown to this build", status.Unknown).ConfigureAwait(false);
        }

        await output.WriteLineAsync(status.UpToDate ? "Schema is up to date." : "Schema is not up to date.").ConfigureAwait(false);
        return ExitOk;
    }



    private static async Task<int> ScriptAsync(MigrationRunner runner, MigrateCommandLine command, TextWriter output, CancellationToken cancellationToken)
    {
        var script = runner.Script(command.From, command.To);
        if (command.Output is null)
        {
            await output.WriteAsync(script).ConfigureAwait(false);
            return ExitOk;
        }

        await File.WriteAllTextAsync(command.Output, script, cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync("Wrote " + command.Output).ConfigureAwait(false);
        return ExitOk;
    }



    private static async Task<int> ApplyAsync(MigrationRunner runner, MigrateCommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var result = await runner.ApplyAsync(command.To, command.ConfirmDataLoss, cancellationToken).ConfigureAwait(false);
        if (result.RequiresConfirmation)
        {
            await error.WriteLineAsync("This downgrade loses data; re-run with --confirm-data-loss to proceed:").ConfigureAwait(false);
            foreach (var step in result.DestructiveSteps)
            {
                await error.WriteLineAsync("  " + step).ConfigureAwait(false);
            }

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



    private static async Task WriteListAsync(TextWriter output, string title, IReadOnlyList<string> items)
    {
        await output.WriteLineAsync($"{title} ({items.Count}):").ConfigureAwait(false);
        foreach (var item in items)
        {
            await output.WriteLineAsync("  " + item).ConfigureAwait(false);
        }
    }
}
