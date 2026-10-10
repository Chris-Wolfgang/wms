// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using System.IO.Pipes;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;
using Wolfgang.Wms.Core.Identity.Providers;
using Wolfgang.Wms.Infrastructure.Secrets;

namespace Wolfgang.Wms.Admin;

/// <summary>
/// The <c>wms-admin</c> tool (E9.3), testable in-process: parses the command line, opens the host's file key
/// ring, seals one request, sends it down the host-only pipe and prints the answer. It never touches the web
/// API or the database.
/// </summary>
public static class AdminProgram
{
    /// <summary>The command ran.</summary>
    public const int ExitOk = 0;

    /// <summary>The host refused the command; the reason is in the output.</summary>
    public const int ExitRefused = 1;

    /// <summary>Usage or configuration error.</summary>
    public const int ExitUsage = 2;

    /// <summary>The host did not answer on the channel.</summary>
    public const int ExitUnreachable = 3;



    /// <summary>
    /// How long to wait for the host to accept the connection.
    /// </summary>
    public static TimeSpan ConnectTimeout { get; } = TimeSpan.FromSeconds(5);



    /// <summary>
    /// Runs the tool.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="output">Standard output.</param>
    /// <param name="error">Standard error.</param>
    /// <param name="configuration">Configuration providing <c>Wms:Admin</c> and <c>Wms:DataProtection</c>; null reads appsettings.json and the environment.</param>
    /// <param name="timeProvider">The clock the request is stamped with.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The process exit code.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output, TextWriter error, IConfiguration? configuration, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var command = AdminCommandLine.Parse(args);
        if (command.Help)
        {
            await output.WriteLineAsync(AdminCommandLine.Usage).ConfigureAwait(false);
            return ExitOk;
        }

        if (command.Error is not null)
        {
            await error.WriteLineAsync(command.Error).ConfigureAwait(false);
            await error.WriteLineAsync(AdminCommandLine.Usage).ConfigureAwait(false);
            return ExitUsage;
        }

        configuration ??= new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var keyRing = command.KeyRing ?? configuration[KeyRingOptions.PathKey];
        if (await KeyRingProblemAsync(keyRing, error).ConfigureAwait(false))
        {
            return ExitUsage;
        }

        var channel = command.Channel ?? configuration[AdminChannelOptions.NameKey];
        channel = string.IsNullOrWhiteSpace(channel) ? AdminChannelProtocol.DefaultName : channel.Trim();

        var protector = DataProtectionProvider.Create(new DirectoryInfo(keyRing!), options => options.SetApplicationName(KeyRing.ApplicationName)).CreateProtector(AdminChannelProtocol.Purpose);
        var request = new AdminChannelRequest(command.Command!, command.Minutes, OsUser(), timeProvider.GetUtcNow());
        var sealedLine = AdminChannelProtocol.Seal(request, protector);

        var (response, failure) = await TryExchangeAsync(channel, sealedLine, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            await error.WriteLineAsync(failure).ConfigureAwait(false);
            return ExitUnreachable;
        }

        await (response!.Ok ? output : error).WriteLineAsync(response.Message).ConfigureAwait(false);
        if (response.Status is { } status)
        {
            await output.WriteLineAsync($"localLoginOpen={status.LocalLoginOpen} ssoVerified={status.SsoVerified} unlockedUntil={status.UnlockedUntil?.ToString("O", CultureInfo.InvariantCulture) ?? "-"} forcedLocal={status.ForcedLocal}").ConfigureAwait(false);
        }

        return response.Ok ? ExitOk : ExitRefused;
    }



    /// <summary>
    /// True (after explaining on <paramref name="error"/>) when there is no usable file key ring.
    /// </summary>
    private static async Task<bool> KeyRingProblemAsync(string? keyRing, TextWriter error)
    {
        if (string.IsNullOrWhiteSpace(keyRing))
        {
            await error.WriteLineAsync($"No key ring directory: set {KeyRingOptions.PathKey} or pass --key-ring. A host that keeps its ring in the database has no file ring for this tool to seal requests with; use {AuthProviderSettings.ForceLocalKey} on the host instead.").ConfigureAwait(false);
            return true;
        }

        if (!Directory.Exists(keyRing))
        {
            await error.WriteLineAsync($"Key ring directory not found: {keyRing}").ConfigureAwait(false);
            return true;
        }

        return false;
    }



    /// <summary>
    /// The host's response, or the message for the operator when the exchange failed.
    /// </summary>
    private static async Task<(AdminChannelResponse? Response, string? Failure)> TryExchangeAsync(string channel, string sealedLine, CancellationToken cancellationToken)
    {
        try
        {
            var response = await ExchangeAsync(channel, sealedLine, cancellationToken).ConfigureAwait(false);
            return response is null
                ? (null, $"The host answered on channel '{channel}' with something that is not a channel response.")
                : (response, null);
        }
        catch (TimeoutException)
        {
            return (null, $"The host did not answer on channel '{channel}' within {ConnectTimeout.TotalSeconds:0} seconds. Is the API running on this machine, and is {AdminChannelOptions.NameKey} the same on both sides?");
        }
        catch (IOException exception)
        {
            return (null, $"The channel '{channel}' failed: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return (null, $"Access to channel '{channel}' was denied: {exception.Message}. Run the tool as the service account or a local administrator.");
        }
    }



    /// <summary>
    /// The user running the tool as the operating system names them; a Windows pipe also reports it to the
    /// host independently, which is the name the host prefers.
    /// </summary>
    public static string OsUser()
    {
        var name = Environment.UserName;
        return OperatingSystem.IsWindows() && !string.IsNullOrEmpty(Environment.UserDomainName)
            ? Environment.UserDomainName + "\\" + name
            : name;
    }



    private static async Task<AdminChannelResponse?> ExchangeAsync(string channel, string sealedLine, CancellationToken cancellationToken)
    {
        using var pipe = new NamedPipeClientStream(".", channel, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync((int)ConnectTimeout.TotalMilliseconds, cancellationToken).ConfigureAwait(false);

        var bytes = Encoding.UTF8.GetBytes(sealedLine + "\n");
        await pipe.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);

        using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return AdminChannelProtocol.Decode(line);
    }
}
