// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Identity.External;

namespace Wolfgang.Wms.Auth.Oidc;

/// <summary>
/// The handler events (E11.1): a validated token becomes a console session through
/// <see cref="IExternalAccounts"/> (account created on first sign-in, roles from the group mappings); a
/// refused account or a provider failure answers a problem instead of an exception page.
/// </summary>
public static partial class OidcEvents
{
    /// <summary>
    /// Replaces the provider's principal with the console session principal, or answers a problem.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var services = context.HttpContext.RequestServices;
        var provider = services.GetRequiredService<OidcAuthProvider>();
        var identity = provider.Identify(context.Principal ?? new System.Security.Claims.ClaimsPrincipal());
        if (identity.Subject.Length == 0)
        {
            await RefuseAsync(context, AuthErrorCodes.ProviderFailed, "the token carries no subject").ConfigureAwait(false);
            return;
        }

        var result = await services.GetRequiredService<IExternalAccounts>().SignInAsync(provider.Name, identity, context.HttpContext.RequestAborted).ConfigureAwait(false);
        switch (result.Outcome)
        {
            case ExternalSignInOutcome.Success:
                context.Principal = SessionClaims.Principal(result.User!);
                context.Properties ??= new AuthenticationProperties();
                context.Properties.IsPersistent = false;
                break;
            case ExternalSignInOutcome.Disabled:
                await RefuseAsync(context, AuthErrorCodes.Disabled, detail: null).ConfigureAwait(false);
                break;
            default:
                await RefuseAsync(context, AuthErrorCodes.IntegrityFailure, detail: null).ConfigureAwait(false);
                break;
        }
    }



    /// <summary>
    /// A failure talking to the provider (state mismatch, token error, discovery down) answers a 502 problem.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public static Task OnRemoteFailureAsync(RemoteFailureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(OidcEvents));
        LogRemoteFailure(logger, context.Failure);
        context.HandleResponse();
        return ApiProblems.Problem(AuthErrorCodes.ProviderFailed, detail: null, context.Failure?.Message ?? "unknown error").ExecuteAsync(context.HttpContext);
    }



    private static Task RefuseAsync(TokenValidatedContext context, Wolfgang.Wms.Domain.Keys.ErrorCode code, string? detail)
    {
        context.HandleResponse();
        return ApiProblems.Problem(code, detail, detail ?? string.Empty).ExecuteAsync(context.HttpContext);
    }



    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC sign-in failed at the provider.")]
    private static partial void LogRemoteFailure(ILogger logger, Exception? exception);
}
