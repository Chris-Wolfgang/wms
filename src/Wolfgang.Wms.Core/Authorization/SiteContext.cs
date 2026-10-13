// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// Which site a request acts on (E10.3): the <c>siteId</c> route value when the endpoint has one, else the
/// <c>X-Wms-Site</c> header the console sends for the site it is showing, else none (an organisation-wide
/// request, which only organisation grants satisfy).
/// </summary>
public static class SiteContext
{
    /// <summary>
    /// The header naming the site a console request acts on.
    /// </summary>
    public const string Header = "X-Wms-Site";



    /// <summary>
    /// The route value naming the site.
    /// </summary>
    public const string RouteValue = "siteId";



    /// <summary>
    /// The site of the request, or null.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="httpContext"/> is null.</exception>
    public static long? SiteIdOf(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.GetRouteValue(RouteValue) is string route && long.TryParse(route, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromRoute))
        {
            return fromRoute;
        }

        return long.TryParse(httpContext.Request.Headers[Header].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromHeader) ? fromHeader : null;
    }
}
