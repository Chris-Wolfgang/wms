// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Identity;

namespace Wolfgang.Wms.Core.Logging;

/// <summary>
/// Correlation (E12.3): every log line written while a request runs carries the trace id, the signed-in
/// user, the device (<see cref="DeviceHeader"/>) and the business identifiers in the route (tote, release,
/// deposit run, picker, site, device) as properties, through a logger scope, so a support engineer can
/// pull every line of one tote or one run. Place it after authentication.
/// </summary>
public static class WmsCorrelation
{
    /// <summary>
    /// The header a handheld sends with its device identifier.
    /// </summary>
    public const string DeviceHeader = "X-Wms-Device";



    /// <summary>
    /// The route values copied into the scope, by their route parameter name.
    /// </summary>
    public static IReadOnlyList<string> RouteKeys { get; } = ["toteId", "releaseId", "depositRunId", "runId", "pickerId", "siteId", "deviceId", "userId", "roleId"];



    /// <summary>
    /// Adds the scope middleware.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseWmsCorrelation(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetService(typeof(ILogger<HttpContext>)) as ILogger;
            if (logger is null)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            using (logger.BeginScope(Properties(context)))
            {
                await next(context).ConfigureAwait(false);
            }
        });
    }



    /// <summary>
    /// The properties of a request: trace id, user, device and the known route identifiers present.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public static Dictionary<string, object> Properties(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var properties = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["TraceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
        };
        if (SessionClaims.UserIdOf(context.User) is { } userId)
        {
            properties["UserId"] = userId;
        }

        if (context.User.Identity?.Name is { Length: > 0 } user)
        {
            properties["User"] = user;
        }

        if (context.Request.Headers.TryGetValue(DeviceHeader, out var device) && device.ToString() is { Length: > 0 } deviceId)
        {
            properties["Device"] = deviceId;
        }

        var route = context.GetRouteData()?.Values;
        if (route is not null)
        {
            foreach (var key in RouteKeys)
            {
                if (route.TryGetValue(key, out var value) && value is not null)
                {
                    properties[char.ToUpperInvariant(key[0]) + key[1..]] = value;
                }
            }
        }

        return properties;
    }
}
