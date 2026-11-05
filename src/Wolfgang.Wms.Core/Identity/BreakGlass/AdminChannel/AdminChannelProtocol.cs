// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;

/// <summary>
/// The wire format of the admin channel (E9.3): one line per message. A request is JSON sealed with the
/// host's Data Protection ring under <see cref="Purpose"/>, so only a tool that can open the same ring can
/// issue one; the host refuses a request issued more than <see cref="MaxSkew"/> ago or ahead. A response is
/// plain JSON: the pipe itself is host-only and the answer carries no secret.
/// </summary>
public static class AdminChannelProtocol
{
    /// <summary>
    /// The Data Protection purpose of a request; changing it would make every tool and host disagree.
    /// </summary>
    public const string Purpose = "Wolfgang.Wms.AdminChannel.v1";



    /// <summary>
    /// The pipe name when <c>Wms:Admin:ChannelName</c> is not set.
    /// </summary>
    public const string DefaultName = "Wolfgang.Wms.Admin";



    /// <summary>
    /// How far a request's <see cref="AdminChannelRequest.IssuedAt"/> may be from the host's clock.
    /// </summary>
    public static TimeSpan MaxSkew { get; } = TimeSpan.FromMinutes(2);



    /// <summary>
    /// A request as the tool sends it: JSON sealed by <paramref name="protector"/>, one line, no newline.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static string Seal(AdminChannelRequest request, IDataProtector protector)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(protector);

        return protector.Protect(JsonSerializer.Serialize(request, AdminChannelJsonContext.Default.AdminChannelRequest));
    }



    /// <summary>
    /// The request inside a sealed line, or an <see cref="InvalidOperationException"/> naming why it is refused:
    /// not sealed with this host's ring, malformed, an unknown command, or issued outside <see cref="MaxSkew"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">The line is not an acceptable request.</exception>
    public static AdminChannelRequest Open(string sealedLine, IDataProtector protector, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sealedLine);
        ArgumentNullException.ThrowIfNull(protector);

        string json;
        try
        {
            json = protector.Unprotect(sealedLine.Trim());
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("The request was not sealed with this host's key ring (point wms-admin at the ring the host uses).", exception);
        }

        AdminChannelRequest? request;
        try
        {
            request = JsonSerializer.Deserialize(json, AdminChannelJsonContext.Default.AdminChannelRequest);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The request is not a channel message.", exception);
        }

        if (request is null || string.IsNullOrWhiteSpace(request.OsUser))
        {
            throw new InvalidOperationException("The request is not a channel message.");
        }

        if (!AdminChannelCommands.IsKnown(request.Command))
        {
            throw new InvalidOperationException($"'{request.Command}' is not a channel command.");
        }

        if ((now - request.IssuedAt).Duration() > MaxSkew)
        {
            throw new InvalidOperationException($"The request was issued at {request.IssuedAt:O}, outside the {MaxSkew.TotalMinutes:0}-minute window; check the clocks and retry.");
        }

        return request;
    }



    /// <summary>
    /// A response as the host sends it: one line of JSON.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public static string Encode(AdminChannelResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return JsonSerializer.Serialize(response, AdminChannelJsonContext.Default.AdminChannelResponse);
    }



    /// <summary>
    /// The response in a line, or null when the line is not one.
    /// </summary>
    public static AdminChannelResponse? Decode(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(line, AdminChannelJsonContext.Default.AdminChannelResponse);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
