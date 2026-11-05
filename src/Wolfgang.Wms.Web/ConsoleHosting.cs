// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.HttpOverrides;

namespace Wolfgang.Wms.Web;

/// <summary>
/// How the console is fronted (E10.6): the same <c>Wms:Hosting:BehindProxy</c> key as the API, honoured the
/// same way. The console cannot reference Core (it talks to the API only), so the rule is repeated here.
/// </summary>
public static class ConsoleHosting
{
    /// <summary>
    /// The configuration key that says a reverse proxy fronts the console.
    /// </summary>
    public const string BehindProxyKey = "Wms:Hosting:BehindProxy";



    /// <summary>
    /// Behind a proxy: forward proto, host and client address from any proxy; otherwise forward nothing.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static void ConfigureForwardedHeaders(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!string.Equals(configuration[BehindProxyKey], "true", StringComparison.OrdinalIgnoreCase))
        {
            options.ForwardedHeaders = ForwardedHeaders.None;
            return;
        }

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        options.ForwardLimit = 2;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
}
