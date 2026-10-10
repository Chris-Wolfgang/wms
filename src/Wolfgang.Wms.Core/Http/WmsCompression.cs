// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;

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
    /// Adds request decompression and response compression, the latter on a branch that a request to a marked
    /// endpoint bypasses: the compression middleware never runs for it, on HTTP or HTTPS, so it installs no
    /// compression feature and nothing downstream has to switch one off, and the request itself is left as the
    /// client sent it. The opt-out reads the endpoint that routing selected, so call this after routing has run
    /// (after <c>UseRouting</c>; a <c>WebApplication</c> that never calls it gets that call at the start of its
    /// pipeline) and before the endpoints execute.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseWmsCompression(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseRequestDecompression();
        app.UseWhen(context => !IsOptedOut(context), branch => branch.UseResponseCompression());
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
