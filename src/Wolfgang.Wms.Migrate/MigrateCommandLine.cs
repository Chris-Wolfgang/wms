// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Migrate;

/// <summary>
/// The declarative command surface of <c>wms-migrate</c> (E4.6): no arguments applies the latest migration;
/// <c>--to</c> moves up or down to a target; <c>--script</c> emits SQL; <c>--status</c> reports. Provider and
/// connection come from configuration or the flags. No <c>--up</c>/<c>--down</c>: the tool states the direction.
/// </summary>
public sealed record MigrateCommandLine
{
    /// <summary>
    /// Text for <c>--help</c>.
    /// </summary>
    public const string Usage = """
        wms-migrate [migrate] [options]

          (no mode)                  apply pending migrations up to --to or the latest
          --status                   list applied and pending migrations and the version this build expects
          --script                   write idempotent SQL from --from (or an empty schema) to --to (or latest);
                                     needs no database connection
          --to <migration>           target: a migration id, its name, its timestamp prefix, or 0 (empty)
          --from <migration>         start of a --script delta
          --output <file>            write the script to a file instead of standard output
          --confirm-data-loss        allow a downgrade that drops tables, columns, schemas or rows
          --provider <SqlServer|PostgreSql>   overrides Wms:Database:Provider
          --connection-string <cs>   overrides Wms:Database:ConnectionString
          --trust-server-certificate overrides Wms:Database:TrustServerCertificate (SQL Server)
          --key-ring <path>          overrides Wms:DataProtection:KeyRingPath (decrypts an enc:v1: connection string)
          --protect                  print the connection string (--connection-string or configured) encrypted
                                     with the key ring as enc:v1:..., for appsettings or an environment variable
          --help, -h                 this text

        Configuration is read from appsettings.json in the working directory and Wms__Database__* /
        Wms__DataProtection__* environment variables; flags win. Exit codes: 0 ok, 1 a migration failed (named
        in the output), 2 usage or configuration error, 3 confirmation required.
        """;



    /// <summary>List applied and pending migrations.</summary>
    public bool Status { get; init; }

    /// <summary>Emit SQL instead of applying.</summary>
    public bool Script { get; init; }

    /// <summary>Target migration; null for the latest.</summary>
    public string? To { get; init; }

    /// <summary>Start of a script delta; null for an empty schema.</summary>
    public string? From { get; init; }

    /// <summary>File to write the script to; null for standard output.</summary>
    public string? Output { get; init; }

    /// <summary>Allow a data-losing downgrade.</summary>
    public bool ConfirmDataLoss { get; init; }

    /// <summary>Provider override.</summary>
    public string? Provider { get; init; }

    /// <summary>Connection string override.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>SQL Server certificate trust override.</summary>
    public bool? TrustServerCertificate { get; init; }



    /// <summary>
    /// <c>--key-ring</c>: the key ring directory, overriding <c>Wms:DataProtection:KeyRingPath</c>.
    /// </summary>
    public string? KeyRing { get; init; }



    /// <summary>
    /// <c>--protect</c>: encrypt the connection string and print it instead of migrating.
    /// </summary>
    public bool Protect { get; init; }

    /// <summary>Show usage.</summary>
    public bool Help { get; init; }

    /// <summary>Usage error, or null when the arguments parsed.</summary>
    public string? Error { get; init; }



    /// <summary>
    /// Parses the arguments; a leading <c>migrate</c> word (from <c>wms migrate …</c>) is ignored.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
    public static MigrateCommandLine Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var result = new MigrateCommandLine();
        var i = args.Count > 0 && string.Equals(args[0], "migrate", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        for (; i < args.Count; i++)
        {
            var argument = args[i].ToLowerInvariant();
            string? error = null;
            var value = argument is "--to" or "--from" or "--output" or "--provider" or "--connection-string" or "--key-ring" ? Value(args, ref i, out error) : null;
            if (error is not null)
            {
                return result with { Error = error };
            }

            result = argument switch
            {
                "--status" => result with { Status = true },
                "--script" => result with { Script = true },
                "--confirm-data-loss" => result with { ConfirmDataLoss = true },
                "--trust-server-certificate" => result with { TrustServerCertificate = true },
                "--help" or "-h" => result with { Help = true },
                "--to" => result with { To = value },
                "--from" => result with { From = value },
                "--output" => result with { Output = value },
                "--provider" => result with { Provider = value },
                "--connection-string" => result with { ConnectionString = value },
                "--key-ring" => result with { KeyRing = value },
                "--protect" => result with { Protect = true },
                _ => result with { Error = $"Unknown argument '{args[i]}'." },
            };

            if (result.Error is not null)
            {
                return result;
            }
        }

        if ((result.Status && result.Script) || (result.Protect && (result.Status || result.Script)))
        {
            return result with { Error = "--status, --script and --protect cannot be combined." };
        }

        if (result.From is not null && !result.Script)
        {
            return result with { Error = "--from applies to --script only." };
        }

        return result;
    }



    private static string? Value(IReadOnlyList<string> args, ref int i, out string? error)
    {
        error = null;
        if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            error = $"{args[i]} needs a value.";
            return null;
        }

        i++;
        return args[i];
    }
}
