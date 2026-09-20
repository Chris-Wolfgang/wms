// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wolfgang.Wms.Core.Jobs;

namespace Wolfgang.Wms.Core.Hosting;

/// <summary>
/// The probes (E12.1): <c>/health/live</c> answers 200 while the process serves requests (no checks);
/// <c>/health/ready</c> runs every check tagged <see cref="ReadyTag"/> (the database: reachable and at the
/// build's schema) and answers 503 when one is unhealthy. Both are outside the versioned API, anonymous,
/// and answer JSON with one line per check. Plain HTTP is allowed on them (probes seldom carry TLS).
/// </summary>
public static class WmsHealth
{
    /// <summary>The liveness route.</summary>
    public const string LiveRoute = "/health/live";

    /// <summary>The readiness route.</summary>
    public const string ReadyRoute = "/health/ready";

    /// <summary>The prefix both routes share.</summary>
    public const string Prefix = "/health";

    /// <summary>The tag a check carries to take part in readiness.</summary>
    public const string ReadyTag = "ready";



    /// <summary>
    /// Registers the health service; checks are added by the components that own them.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsHealth(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHealthChecks();
        services.TryAddSingleton<ILeaderLock, NoLeaderLock>();   // E12.6: a host without a database is its own leader
        return services;
    }



    /// <summary>
    /// Maps the two probes on the host root.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IEndpointRouteBuilder MapWmsHealth(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks(LiveRoute, new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteAsync }).AllowAnonymous();
        app.MapHealthChecks(ReadyRoute, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag), ResponseWriter = WriteAsync }).AllowAnonymous();
        return app;
    }



    /// <summary>
    /// The JSON body: the overall status and one entry per check (name, status, description, duration).
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        using var buffer = new MemoryStream();
        var writer = new Utf8JsonWriter(buffer);
        await using (writer.ConfigureAwait(false))
        {
            writer.WriteStartObject();
            writer.WriteString("status", report.Status.ToString());
            writer.WriteString("totalDuration", report.TotalDuration.ToString("c", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteStartArray("checks");
            foreach (var (name, entry) in report.Entries)
            {
                writer.WriteStartObject();
                writer.WriteString("name", name);
                writer.WriteString("status", entry.Status.ToString());
                writer.WriteString("description", entry.Description ?? entry.Exception?.Message ?? string.Empty);
                writer.WriteString("duration", entry.Duration.ToString("c", System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        await context.Response.Body.WriteAsync(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), context.RequestAborted).ConfigureAwait(false);
    }
}
