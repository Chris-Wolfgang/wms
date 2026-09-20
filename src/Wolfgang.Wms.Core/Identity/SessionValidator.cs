// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// The rules a console session lives by (E10.5): an absolute lifetime and an idle timeout from settings, and
/// per-user revocation. Runs on every request that presents the cookie (<c>OnValidatePrincipal</c>) and at
/// sign-in (<c>OnSigningIn</c>); a rejected session is signed out and the request continues anonymously,
/// so the endpoint answers <c>401 auth.not_signed_in</c>.
/// </summary>
public static class SessionValidator
{
    /// <summary>
    /// The claim carrying when the session was first issued (ISO 8601), the anchor of the absolute lifetime
    /// and of revocation; sliding renewal never moves it.
    /// </summary>
    public const string SignedInAtClaim = "wms:signed_in_at";



    /// <summary>
    /// At sign-in: stamps the issue time and sets the idle expiry from settings.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public static async Task OnSigningInAsync(CookieSigningInContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var services = context.HttpContext.RequestServices;
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var idle = await services.GetRequiredService<ISettings>().GetAsync(AuthSettings.IdleTimeout, SettingScopeRef.Organization, context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (context.Principal?.Identity is ClaimsIdentity identity && identity.FindFirst(SignedInAtClaim) is null)
        {
            identity.AddClaim(new Claim(SignedInAtClaim, now.ToString("O", CultureInfo.InvariantCulture)));
        }

        context.Properties.IssuedUtc = now;
        context.Properties.ExpiresUtc = now + idle;
        context.Properties.AllowRefresh = true;
    }



    /// <summary>
    /// On every request: rejects a session past its absolute lifetime or issued before the user's
    /// "sessions valid after"; otherwise lets sliding renewal extend the idle expiry.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public static async Task OnValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var services = context.HttpContext.RequestServices;
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var reason = await ReasonToRejectAsync(context.Principal, now, services.GetRequiredService<ISettings>(), services.GetRequiredService<ISessionRevocations>(), context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (reason is null)
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
    }



    /// <summary>
    /// Why a session must end, or null while it is valid: no issue time, past the absolute lifetime, or
    /// issued before the user's "sessions valid after".
    /// </summary>
    public static async Task<string?> ReasonToRejectAsync(ClaimsPrincipal? principal, DateTimeOffset now, ISettings settings, ISessionRevocations revocations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(revocations);

        if (!DateTimeOffset.TryParseExact(principal?.FindFirst(SignedInAtClaim)?.Value, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var signedInAt))
        {
            return "The session carries no issue time.";
        }

        var lifetime = await settings.GetAsync(AuthSettings.SessionLifetime, SettingScopeRef.Organization, cancellationToken).ConfigureAwait(false);
        if (now - signedInAt > lifetime)
        {
            return "The session is past its absolute lifetime.";
        }

        var userId = SessionClaims.UserIdOf(principal);
        if (userId is null)
        {
            return "The session names no user.";
        }

        var validAfter = await revocations.SessionsValidAfterAsync(userId.Value, cancellationToken).ConfigureAwait(false);
        return validAfter is { } after && signedInAt < after ? "The session was issued before the user's sessions were revoked." : null;
    }
}
