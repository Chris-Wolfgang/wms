// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Identity.Providers;

namespace Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;

/// <summary>
/// Runs one admin-channel command against the gate (E9.3), independent of the transport so it is testable
/// without a pipe: opens the sealed request with the host's ring, prefers the OS user the transport
/// authenticated over the one the tool claims, and answers with the gate's status. Every command and every
/// refusal is logged at Warning, because each one changes (or tries to change) who can sign in.
/// </summary>
public sealed partial class AdminChannelHandler
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IDataProtectionProvider _dataProtection;
    private readonly AuthProviderState _providers;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AdminChannelHandler> _logger;



    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public AdminChannelHandler(IServiceScopeFactory scopes, IDataProtectionProvider dataProtection, AuthProviderState providers, TimeProvider timeProvider, ILogger<AdminChannelHandler> logger)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _dataProtection = dataProtection ?? throw new ArgumentNullException(nameof(dataProtection));
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <summary>
    /// Handles one sealed request line. <paramref name="connectedOsUser"/> is the user the transport
    /// authenticated (a Windows pipe knows it); null when the transport cannot tell, in which case the
    /// request's own claim is recorded.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="sealedLine"/> is null.</exception>
    public async Task<AdminChannelResponse> HandleAsync(string sealedLine, string? connectedOsUser, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sealedLine);

        var now = _timeProvider.GetUtcNow();
        AdminChannelRequest request;
        try
        {
            request = AdminChannelProtocol.Open(sealedLine, _dataProtection.CreateProtector(AdminChannelProtocol.Purpose), now);
        }
        catch (InvalidOperationException exception)
        {
            LogRefused(_logger, exception.Message);
            return new AdminChannelResponse(Ok: false, exception.Message, Status: null);
        }

        var actor = string.IsNullOrWhiteSpace(connectedOsUser) ? request.OsUser : connectedOsUser;
        using var scope = _scopes.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<ILocalLoginGate>();
        try
        {
            var info = request.Command switch
            {
                AdminChannelCommands.Unlock => await gate.UnlockAsync(TimeSpan.FromMinutes(request.Minutes ?? LocalLoginGateRules.DefaultWindow.TotalMinutes), actor, cancellationToken).ConfigureAwait(false),
                AdminChannelCommands.Lock => await gate.LockAsync(actor, cancellationToken).ConfigureAwait(false),
                _ => await gate.GetAsync(cancellationToken).ConfigureAwait(false),
            };
            var status = info.ToStatus(now, _providers.ForceLocal);
            LogCommand(_logger, request.Command, actor, status.LocalLoginOpen);
            return new AdminChannelResponse(Ok: true, Describe(request.Command, actor, status), status);
        }
        catch (AuthException exception)
        {
            LogRefused(_logger, exception.Message);
            return new AdminChannelResponse(Ok: false, exception.Message, Status: null);
        }
        catch (ArgumentException exception)
        {
            LogRefused(_logger, exception.Message);
            return new AdminChannelResponse(Ok: false, exception.Message, Status: null);
        }
    }



    private static string Describe(string command, string actor, LocalLoginStatus status)
    {
        var until = status.UnlockedUntil?.ToString("O", CultureInfo.InvariantCulture);
        return command switch
        {
            AdminChannelCommands.Unlock => $"Local sign-in unlocked until {until} by {actor}.",
            AdminChannelCommands.Lock when status.LocalLoginOpen => "Window closed; local sign-in stays open for another reason (SSO not verified, or ForceLocal).",
            AdminChannelCommands.Lock => "Window closed; local sign-in is closed.",
            _ when !status.LocalLoginOpen => "Local sign-in is closed (single sign-on verified, no window open).",
            _ when until is null => "Local sign-in is open.",
            _ => $"Local sign-in is open until {until}.",
        };
    }



    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin channel: '{Command}' by '{Actor}'; local sign-in open: {Open}.")]
    private static partial void LogCommand(ILogger logger, string command, string actor, bool open);



    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin channel request refused: {Reason}")]
    private static partial void LogRefused(ILogger logger, string reason);
}
