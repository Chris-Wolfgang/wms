// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Wolfgang.Wms.Core.Http;

/// <summary>
/// Compression conventions (E82.3): responses are compressed with Brotli (gzip fallback) for JSON, XML,
/// problem details and text; requests may arrive gzip- or Brotli-compressed (devices and imports); endpoints
/// marked <see cref="DisableResponseCompression"/> (authentication) send uncompressed bodies whatever the
/// request scheme, so a secret can never be recovered by a BREACH-style attack. The scheme must not matter:
/// TLS ends at a reverse proxy in the documented deployments, so Kestrel sees plain HTTP for a response the
/// proxy then encrypts, which is exactly the BREACH condition.
/// </summary>
public static class WmsCompression
{
    /// <summary>
    /// Media types whose bodies are compressed. Binary and already-compressed types are left alone.
    /// </summary>
    public static IReadOnlyList<string> CompressedMediaTypes { get; } =
    [
        "application/json",
        "application/problem+json",
        "application/xml",
        "application/problem+xml",
        "text/plain",
        "text/csv",
    ];



    /// <summary>
    /// Registers response compression (Brotli then gzip, HTTPS included) and request decompression.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsCompression(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRequestDecompression();
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = CompressedMediaTypes;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        return services;
    }



    /// <summary>
    /// Adds request decompression, the per-endpoint opt-out, and response compression. For a marked endpoint the
    /// opt-out drops the request's <c>Accept-Encoding</c> before the compression middleware looks at it, so the
    /// middleware never engages, on HTTP or HTTPS; a second step inside the middleware also switches its
    /// <see cref="IHttpsCompressionFeature"/> to <see cref="HttpsCompressionMode.DoNotCompress"/>, which is what the
    /// framework consults for an HTTPS request. Call before endpoints.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseWmsCompression(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseRequestDecompression();
        app.Use(next => context =>
        {
            if (IsOptedOut(context))
            {
                context.Request.Headers.Remove(HeaderNames.AcceptEncoding);
            }

            return next(context);
        });
        app.UseResponseCompression();
        app.Use(next => context =>
        {
            if (IsOptedOut(context))
            {
                var feature = context.Features.Get<IHttpsCompressionFeature>();
                if (feature is not null)
                {
                    feature.Mode = HttpsCompressionMode.DoNotCompress;
                }
            }

            return next(context);
        });
        return app;
    }



    /// <summary>
    /// Marks an endpoint (or group) whose responses must never be compressed: anything that echoes a secret or
    /// a token next to attacker-influenced content, which is every authentication endpoint.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static TBuilder DisableResponseCompression<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithMetadata(DisableResponseCompressionMetadata.Instance);
        return builder;
    }



    private static bool IsOptedOut(HttpContext context)
    {
        return context.GetEndpoint()?.Metadata.GetMetadata<DisableResponseCompressionMetadata>() is not null;
    }
}
