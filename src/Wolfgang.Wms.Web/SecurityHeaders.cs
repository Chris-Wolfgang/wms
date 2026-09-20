// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Primitives;

namespace Wolfgang.Wms.Web;

/// <summary>
/// The console's security headers (E10.6): a content-security policy tuned for a Blazor Web App in Server
/// render mode (scripts and styles from this origin, inline styles for component styling, the circuit's
/// WebSocket, no framing), plus <c>X-Content-Type-Options</c>, <c>Referrer-Policy</c> and a minimal
/// <c>Permissions-Policy</c>. Set on every response.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// The content-security policy.
    /// </summary>
    public const string ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self' ws: wss:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";



    /// <summary>
    /// The headers and their values.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Content-Security-Policy"] = ContentSecurityPolicy,
        ["X-Content-Type-Options"] = "nosniff",
        ["Referrer-Policy"] = "strict-origin-when-cross-origin",
        ["X-Frame-Options"] = "DENY",
        ["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()",
    };



    /// <summary>
    /// Adds the headers to every response.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseWmsSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use((context, next) =>
        {
            context.Response.OnStarting(static state =>
            {
                var response = ((HttpContext)state).Response;
                foreach (var (name, value) in Headers)
                {
                    response.Headers[name] = new StringValues(value);
                }

                return Task.CompletedTask;
            }, context);
            return next(context);
        });
    }
}
