// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;

/// <summary>
/// The host-only channel (E9.3): a named pipe (a Unix domain socket off Windows) that only a process on this
/// machine can reach, so <c>wms-admin</c> run over RDP, SSH or <c>docker exec</c> can open or close the
/// break-glass gate while nothing on the network can. On Windows the pipe is created with an access list of
/// the service account, local administrators and SYSTEM, and the connected user's name is read from the pipe
/// and recorded instead of the name the tool claims. One request per connection, one line each way.
/// Source-address checks are deliberately absent: proxies and containers make them unreliable.
/// </summary>
public sealed partial class AdminChannelServer : BackgroundService
{
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromSeconds(5);

    private readonly AdminChannelHandler _handler;
    private readonly AdminChannelOptions _options;
    private readonly ILogger<AdminChannelServer> _logger;



    /// <summary>
    /// Creates the server.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public AdminChannelServer(AdminChannelHandler handler, IOptions<AdminChannelOptions> options, ILogger<AdminChannelServer> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            LogDisabled(_logger);
            return;
        }

        LogListening(_logger, _options.ChannelName);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var pipe = Create(_options.ChannelName);
                await pipe.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                await ServeAsync(pipe, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException exception)
            {
                LogConnectionFailed(_logger, exception);   // the client dropped mid-request; keep listening
            }
            catch (UnauthorizedAccessException exception)
            {
                LogCannotListen(_logger, _options.ChannelName, exception);
                await Task.Delay(RetryAfterFailure, stoppingToken).ConfigureAwait(false);
            }
        }
    }



    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        var connectedUser = ConnectedUser(pipe);
        using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        var response = line is null
            ? new AdminChannelResponse(Ok: false, "Empty request.", Status: null)
            : await _handler.HandleAsync(line, connectedUser, cancellationToken).ConfigureAwait(false);

        var bytes = Encoding.UTF8.GetBytes(AdminChannelProtocol.Encode(response) + "\n");
        await pipe.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
    }



    /// <summary>
    /// The user on the other end as the operating system reports it (Windows); null where the transport does
    /// not say.
    /// </summary>
    private static string? ConnectedUser(NamedPipeServerStream pipe)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return pipe.GetImpersonationUserName();
        }
        catch (IOException)
        {
            return null;
        }
    }



    private static NamedPipeServerStream Create(string name)
    {
        return OperatingSystem.IsWindows()
            ? CreateOnWindows(name)
            : new NamedPipeServerStream(name, PipeDirection.InOut, maxNumberOfServerInstances: 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
    }



    [SupportedOSPlatform("windows")]
    private static NamedPipeServerStream CreateOnWindows(string name)
    {
        var security = new PipeSecurity();
        if (WindowsIdentity.GetCurrent().User is { } serviceAccount)
        {
            security.AddAccessRule(new PipeAccessRule(serviceAccount, PipeAccessRights.FullControl, AccessControlType.Allow));
        }

        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, domainSid: null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, domainSid: null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, maxNumberOfServerInstances: 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, inBufferSize: 0, outBufferSize: 0, security);
    }



    [LoggerMessage(Level = LogLevel.Information, Message = "Admin channel listening on pipe '{Name}' (host-only).")]
    private static partial void LogListening(ILogger logger, string name);



    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin channel disabled by Wms:Admin:Enabled; Wms:Auth:ForceLocal is the only way to reopen local sign-in.")]
    private static partial void LogDisabled(ILogger logger);



    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin channel connection failed; listening again.")]
    private static partial void LogConnectionFailed(ILogger logger, Exception exception);



    [LoggerMessage(Level = LogLevel.Error, Message = "Admin channel cannot listen on pipe '{Name}' (another process holds it, or access is denied); retrying.")]
    private static partial void LogCannotListen(ILogger logger, string name, Exception exception);
}
