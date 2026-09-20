// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Modules;

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// The <c>auth</c> module (E9): local sign-in with a session cookie, sign-out, who-am-I, and password change.
/// The cookie is protected by the shared Data Protection ring (every instance reads it, E12.6) and answers
/// 401/403 problems rather than redirects (an API, not a site). A user who must change the password can call
/// nothing but the password and sign-out endpoints until done (E9.1). Sign-in is rate-limited per address.
/// </summary>
public static class AuthModule
{
    /// <summary>
    /// The session cookie name.
    /// </summary>
    public const string CookieName = "wms.session";



    /// <summary>
    /// The rate-limiting policy of the sign-in endpoint.
    /// </summary>
    public const string LoginRateLimit = "auth-login";



    /// <summary>
    /// Sign-in attempts allowed per address per minute before 429.
    /// </summary>
    public const int LoginAttemptsPerMinute = 20;



    /// <summary>Route of local sign-in.</summary>
    public const string LoginRoute = "/auth/local/login";

    /// <summary>Route of the password change.</summary>
    public const string PasswordRoute = "/auth/local/password";

    /// <summary>Route of sign-out.</summary>
    public const string LogoutRoute = "/auth/logout";

    /// <summary>Route of who-am-I.</summary>
    public const string MeRoute = "/auth/me";



    /// <summary>
    /// The module descriptor: name <c>auth</c>, the endpoints, the settings, the error codes.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("auth")
        .WithEndpoints(MapEndpoints)
        .WithSettings(AuthSettings.All)
        .WithErrorCodes(AuthErrorCodes.InvalidCredentials, AuthErrorCodes.LockedOut, AuthErrorCodes.Disabled, AuthErrorCodes.NotSignedIn, AuthErrorCodes.Forbidden, AuthErrorCodes.PasswordChangeRequired, AuthErrorCodes.PasswordRejected, AuthErrorCodes.Unavailable);



    /// <summary>
    /// Registers cookie authentication, authorization, the sign-in rate limit, the error handler, the
    /// placeholder accounts (replaced by <c>AddWmsDatabase</c>) and the module.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsAuthModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(ConfigureCookie);
        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginRateLimit, context => RateLimitPartition.GetFixedWindowLimiter
            (
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = LoginAttemptsPerMinute, Window = TimeSpan.FromMinutes(1) }
            ));
        });
        services.AddExceptionHandler<AuthExceptionHandler>();
        services.TryAddScoped<ILocalAccounts, NoLocalAccounts>();
        services.AddWmsModule(Descriptor);
        return services;
    }



    /// <summary>
    /// Adds authentication, authorization, the rate limiter and the must-change-password gate. Call after
    /// <c>UseWmsProblemDetails</c> and before mapping the API.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseWmsAuth(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(RequirePasswordChangeAsync);
        return app;
    }



    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = AuthSettings.SessionLifetime.DefaultValue;   // E10.5 makes it a setting
        options.Events.OnRedirectToLogin = context => ApiProblems.Problem(AuthErrorCodes.NotSignedIn).ExecuteAsync(context.HttpContext);
        options.Events.OnRedirectToAccessDenied = context => ApiProblems.Problem(AuthErrorCodes.Forbidden).ExecuteAsync(context.HttpContext);
    }



    /// <summary>
    /// E9.1: a signed-in user who must change the password reaches only the password and sign-out endpoints.
    /// </summary>
    private static Task RequirePasswordChangeAsync(HttpContext context, RequestDelegate next)
    {
        if (!SessionClaims.MustChangePasswordOf(context.User) || IsExempt(context.Request.Path))
        {
            return next(context);
        }

        return ApiProblems.Problem(AuthErrorCodes.PasswordChangeRequired).ExecuteAsync(context);
    }



    private static bool IsExempt(PathString path)
    {
        return path.Value is { } value
            && (value.EndsWith(PasswordRoute, StringComparison.Ordinal) || value.EndsWith(LogoutRoute, StringComparison.Ordinal) || value.EndsWith(MeRoute, StringComparison.Ordinal));
    }



    private static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost(LoginRoute, LoginAsync)
            .RequireRateLimiting(LoginRateLimit)
            .WithName("LocalLogin")
            .WithSummary("Signs in a local account and sets the session cookie; 401 wrong credentials, 423 locked, 403 disabled.")
            .Produces(StatusCodes.Status200OK);
        app.MapPost(LogoutRoute, () => TypedResults.SignOut(authenticationSchemes: [CookieAuthenticationDefaults.AuthenticationScheme]))
            .WithName("Logout")
            .WithSummary("Ends the session.");
        app.MapGet(MeRoute, (HttpContext http) => TypedResults.Ok(Session(http)))
            .RequireAuthorization()
            .WithName("GetSession")
            .WithSummary("Who is signed in.");
        app.MapPost(PasswordRoute, ChangePasswordAsync)
            .RequireAuthorization()
            .WithName("ChangeLocalPassword")
            .WithSummary("Replaces the signed-in local user's password; clears the must-change flag and refreshes the cookie.")
            .Produces(StatusCodes.Status200OK);
    }



    private static async Task<IResult> LoginAsync(LocalLoginRequest body, ILocalAccounts accounts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        var result = await accounts.LoginAsync(body.UserName ?? string.Empty, body.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            LocalLoginOutcome.Success => TypedResults.SignIn(SessionClaims.Principal(result.User!), new AuthenticationProperties { IsPersistent = false }, CookieAuthenticationDefaults.AuthenticationScheme),
            LocalLoginOutcome.LockedOut => ApiProblems.Problem(AuthErrorCodes.LockedOut, detail: null, result.LockedUntil?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
            LocalLoginOutcome.Disabled => ApiProblems.Problem(AuthErrorCodes.Disabled),
            _ => ApiProblems.Problem(AuthErrorCodes.InvalidCredentials),
        };
    }



    private static async Task<IResult> ChangePasswordAsync(HttpContext http, ChangePasswordRequest body, ILocalAccounts accounts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        var userId = SessionClaims.UserIdOf(http.User) ?? throw new AuthException(AuthErrorCodes.NotSignedIn, "Sign in first.");
        var outcome = await accounts.ChangePasswordAsync(userId, body.CurrentPassword ?? string.Empty, body.NewPassword ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (outcome != PasswordChangeOutcome.Changed)
        {
            var reason = outcome switch
            {
                PasswordChangeOutcome.CurrentPasswordWrong => "The current password is wrong.",
                PasswordChangeOutcome.NewPasswordRejected => PasswordPolicy.Check(body.NewPassword) ?? "The new password was rejected.",
                _ => "The account no longer exists.",
            };
            return ApiProblems.Problem(AuthErrorCodes.PasswordRejected, detail: null, reason);
        }

        var user = await accounts.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        return user is null
            ? TypedResults.SignOut(authenticationSchemes: [CookieAuthenticationDefaults.AuthenticationScheme])
            : TypedResults.SignIn(SessionClaims.Principal(user), new AuthenticationProperties { IsPersistent = false }, CookieAuthenticationDefaults.AuthenticationScheme);   // refresh the claims
    }



    private static SessionInfo Session(HttpContext http)
    {
        var user = http.User;
        return new SessionInfo
        (
            SessionClaims.UserIdOf(user) ?? 0,
            user.FindFirst(SessionClaims.UserName)?.Value ?? string.Empty,
            user.FindFirst(SessionClaims.DisplayName)?.Value ?? string.Empty,
            SessionClaims.MustChangePasswordOf(user),
            string.Equals(user.FindFirst(SessionClaims.LocalAdmin)?.Value, "true", StringComparison.Ordinal)
        );
    }
}
