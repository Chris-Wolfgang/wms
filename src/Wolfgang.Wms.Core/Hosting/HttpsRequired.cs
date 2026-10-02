// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Hosting;

/// <summary>
/// The API refuses plain HTTP (E12.5) with a clear 400 rather than redirecting, so a POST body is never
/// lost to a redirect. Behind a reverse proxy the forwarded scheme (E10.6) is what counts. Exempt: requests
/// from the loopback address (a developer, a local probe), the health probes, and hosts started with
/// <c>Wms:Hosting:AllowHttp=true</c> (a lab). The console redirects instead (<c>UseHttpsRedirection</c>).
/// </summary>
public static class HttpsRequired
{
    /// <summary>
    /// The bootstrap key that allows plain HTTP from any address.
    /// </summary>
    public const string AllowHttpKey = HostingOptions.SectionName + ":AllowHttp";



    /// <summary>
    /// Plain HTTP was used where HTTPS is required.
    /// </summary>
    public static ErrorCode HttpsRequiredCode { get; } = new
    (
        "hosting.https_required",
        StatusCodes.Status400BadRequest,
        "Use https: plain HTTP is refused, not redirected, so request bodies are never lost.",
        "hosting-https-required",
        ErrorSeverity.Error
    );



    /// <summary>
    /// Adds the refusal. Place it right after the forwarded-headers middleware.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IApplicationBuilder UseWmsHttpsRequired(this IApplicationBuilder app, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configuration);

        var allowHttp = bool.TryParse(configuration[AllowHttpKey], out var allow) && allow;
        return app.Use((context, next) => IsAllowed(context, allowHttp) ? next(context) : ApiProblems.Problem(HttpsRequiredCode).ExecuteAsync(context));
    }



    /// <summary>
    /// True when the request may proceed: HTTPS, from the loopback address, a health probe, or a host that
    /// allows plain HTTP.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public static bool IsAllowed(HttpContext context, bool allowHttp)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.IsHttps || allowHttp)
        {
            return true;
        }

        if (context.Request.Path.StartsWithSegments(WmsHealth.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var address = context.Connection.RemoteIpAddress;
        return address is null || IPAddress.IsLoopback(address);
    }
}
