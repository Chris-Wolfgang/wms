// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Wolfgang.Wms.Auth.Oidc;

/// <summary>
/// Builds the handler's options from the provider's snapshot (E11.1): authorization code with PKCE, the
/// session cookie as the sign-in scheme, claims kept as the provider sends them, and the events that turn
/// the provider's principal into a console session or a problem response.
/// </summary>
public sealed class OidcOptionsConfigurator : IConfigureNamedOptions<OpenIdConnectOptions>
{
    /// <summary>
    /// Where the provider sends the browser back (host-relative; the handler answers it itself).
    /// </summary>
    public static PathString CallbackPath { get; } = new("/auth/oidc/callback");

    /// <summary>
    /// Where the provider sends the browser after a sign-out.
    /// </summary>
    public static PathString SignedOutCallbackPath { get; } = new("/auth/oidc/signout-callback");



    private readonly OidcAuthProvider _provider;



    /// <summary>
    /// Creates the configurator.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    public OidcOptionsConfigurator(OidcAuthProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }



    /// <inheritdoc/>
    public void Configure(string? name, OpenIdConnectOptions options)
    {
        if (string.Equals(name, OidcSettings.ProviderName, StringComparison.Ordinal))
        {
            Configure(options);
        }
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public void Configure(OpenIdConnectOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var snapshot = _provider.Snapshot;
        options.Authority = snapshot.Authority.Length > 0 ? snapshot.Authority : null;
        options.ClientId = snapshot.ClientId.Length > 0 ? snapshot.ClientId : null;
        options.ClientSecret = snapshot.ClientSecret.Length > 0 ? snapshot.ClientSecret : null;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.Query;   // the callback is a plain GET redirect, not a form post
        options.UsePkce = true;
        options.RequireHttpsMetadata = snapshot.RequireHttps;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.CallbackPath = CallbackPath;
        options.SignedOutCallbackPath = SignedOutCallbackPath;
        options.SaveTokens = false;
        options.MapInboundClaims = false;   // claim types stay as the provider sends them (sub, groups, ...)
        options.GetClaimsFromUserInfoEndpoint = true;
        options.TokenValidationParameters.NameClaimType = snapshot.NameClaim;
        options.TokenValidationParameters.RoleClaimType = snapshot.GroupClaim;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.NonceCookie.SameSite = SameSiteMode.Lax;
        options.Scope.Clear();
        foreach (var scope in snapshot.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Prepend("openid").Distinct(StringComparer.Ordinal))
        {
            options.Scope.Add(scope);
        }

        options.ClaimActions.Clear();   // MapInboundClaims off and no delete actions: userinfo claims join the id-token claims untouched
        options.Events.OnTokenValidated = OidcEvents.OnTokenValidatedAsync;
        options.Events.OnRemoteFailure = OidcEvents.OnRemoteFailureAsync;
    }
}
