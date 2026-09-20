// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wolfgang.Wms.Core.Hosting;

/// <summary>
/// Honours <c>X-Forwarded-*</c> only when the installer says a proxy fronts the host (E10.6): the request's
/// scheme and client address then come from the proxy, so cookies are marked secure, HTTPS checks pass
/// (E12.5) and rate limits see real addresses. Without the flag the headers are ignored, so a client cannot
/// claim to be behind one.
/// </summary>
public static class ForwardedHeaders
{
    /// <summary>
    /// Binds <c>Wms:Hosting</c> and configures the forwarded-headers middleware from it.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IServiceCollection AddWmsForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var hosting = new HostingOptions { BehindProxy = bool.TryParse(configuration[HostingOptions.BehindProxyKey], out var behindProxy) && behindProxy };   // read directly: Core stays trim- and AOT-clean (no binder)
        services.AddSingleton(hosting);
        services.Configure<ForwardedHeadersOptions>(options => Configure(options, hosting));
        return services;
    }



    /// <summary>
    /// The middleware; first in the pipeline so everything after it sees the forwarded scheme and address.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseWmsForwardedHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseForwardedHeaders();
    }



    /// <summary>
    /// Behind a proxy: forward proto, host and client address from any proxy (the proxy is the only way in);
    /// otherwise forward nothing.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static void Configure(ForwardedHeadersOptions options, HostingOptions hosting)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(hosting);

        if (!hosting.BehindProxy)
        {
            options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.None;
            return;
        }

        options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
        options.ForwardLimit = 2;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
}
