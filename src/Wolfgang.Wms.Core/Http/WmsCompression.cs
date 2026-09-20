// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;

namespace Wolfgang.Wms.Core.Http;

/// <summary>
/// Compression conventions (E82.3): responses are compressed with Brotli (gzip fallback) for JSON, XML,
/// problem details and text; requests may arrive gzip- or Brotli-compressed (devices and imports); endpoints
/// marked <see cref="DisableResponseCompression"/> (authentication) send uncompressed bodies over TLS so a
/// secret can never be recovered by a BREACH-style attack.
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
    /// Adds request decompression, response compression, and the per-endpoint opt-out, in that order: the
    /// opt-out runs inside the compression middleware (which installs the <see cref="IHttpsCompressionFeature"/>
    /// it consults) and switches that feature to <see cref="HttpsCompressionMode.DoNotCompress"/> for a marked
    /// endpoint. Call before endpoints.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseWmsCompression(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseRequestDecompression();
        app.UseResponseCompression();
        app.Use(next => context =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<DisableResponseCompressionMetadata>() is not null)
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
    /// Marks an endpoint (or group) whose responses must not be compressed over TLS: anything that echoes a
    /// secret or a token next to attacker-influenced content, which is every authentication endpoint.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static TBuilder DisableResponseCompression<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithMetadata(DisableResponseCompressionMetadata.Instance);
        return builder;
    }
}
