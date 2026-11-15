// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Wolfgang.Wms.Core.Identity.BreakGlass;
using Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;

namespace Wolfgang.Wms.Admin;

/// <summary>
/// The command surface of <c>wms-admin</c> (E9.3): one verb, a few flags. There is no <c>wms admin</c>
/// subcommand on the NativeAOT CLI; a leading <c>admin</c> word is accepted and ignored so an installer
/// that passes it still works.
/// </summary>
public sealed record AdminCommandLine
{
    /// <summary>
    /// Text for <c>--help</c>.
    /// </summary>
    public const string Usage = """
        wms-admin <command> [options]

          unlock [--minutes N]       open local sign-in for N minutes (default 30, 1 to 43200; never open-ended:
                                     use a large value such as 1200 when a long window is needed)
          lock                       close an open window now
          status                     report whether local sign-in is open, without changing anything

          --channel <name>           the host's pipe name, overriding Wms:Admin:ChannelName (default Wolfgang.Wms.Admin)
          --key-ring <path>          the host's key ring directory, overriding Wms:DataProtection:KeyRingPath;
                                     requests are sealed with it, so only a tool on the host can issue one
          --help, -h                 this text

        Runs on the host (RDP, SSH, docker exec): the channel is a local pipe, never the web API or the database.
        Configuration is read from appsettings.json in the working directory and Wms__* environment variables;
        flags win. Exit codes: 0 ok, 1 the host refused the command (reason printed), 2 usage or configuration
        error, 3 the host did not answer on the channel.
        """;



    /// <summary>The verb: unlock, lock or status; null when none was given.</summary>
    public string? Command { get; init; }

    /// <summary>The window length for unlock; null for the default.</summary>
    public int? Minutes { get; init; }

    /// <summary>Pipe name override.</summary>
    public string? Channel { get; init; }

    /// <summary>Key ring directory override.</summary>
    public string? KeyRing { get; init; }

    /// <summary>Show usage.</summary>
    public bool Help { get; init; }

    /// <summary>Usage error, or null when the arguments parsed.</summary>
    public string? Error { get; init; }



    /// <summary>
    /// Parses the arguments.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
    public static AdminCommandLine Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var result = new AdminCommandLine();
        var i = args.Count > 0 && string.Equals(args[0], "admin", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        for (; i < args.Count; i++)
        {
            var argument = args[i].ToLowerInvariant();
            string? error = null;
            var value = argument is "--minutes" or "--channel" or "--key-ring" ? Value(args, ref i, out error) : null;
            if (error is not null)
            {
                return result with { Error = error };
            }

            result = argument switch
            {
                AdminChannelCommands.Unlock or AdminChannelCommands.Lock or AdminChannelCommands.Status when result.Command is null => result with { Command = argument },
                AdminChannelCommands.Unlock or AdminChannelCommands.Lock or AdminChannelCommands.Status => result with { Error = "Give one command: unlock, lock or status." },
                "--minutes" => WithMinutes(result, value!),
                "--channel" => result with { Channel = value },
                "--key-ring" => result with { KeyRing = value },
                "--help" or "-h" => result with { Help = true },
                _ => result with { Error = $"Unknown argument '{args[i]}'." },
            };

            if (result.Error is not null)
            {
                return result;
            }
        }

        if (result.Help)
        {
            return result;
        }

        if (result.Command is null)
        {
            return result with { Error = "Give a command: unlock, lock or status." };
        }

        if (result.Minutes is not null && !string.Equals(result.Command, AdminChannelCommands.Unlock, StringComparison.Ordinal))
        {
            return result with { Error = "--minutes applies to unlock only." };
        }

        return result;
    }



    private static AdminCommandLine WithMinutes(AdminCommandLine result, string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || LocalLoginGateRules.Validate(TimeSpan.FromMinutes(minutes)) is not null)
        {
            return result with { Error = $"--minutes must be a whole number from {LocalLoginGateRules.MinWindow.TotalMinutes:0} to {LocalLoginGateRules.MaxWindow.TotalMinutes:0}." };
        }

        return result with { Minutes = minutes };
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
