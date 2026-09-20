// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolfgang.Wms.Core.Modules;

namespace Wolfgang.Wms.Core.Schema;

/// <summary>
/// The <c>system</c> module's schema endpoint (E82.5): <c>GET /system/schema</c> under the versioned root,
/// read-only, so an installer or a health check can tell whether <c>wms migrate</c> has run before the API
/// is used. It is the one thing the API says about bootstrap; the steps themselves live outside it
/// (docs/BOOTSTRAP.md).
/// </summary>
public static class SchemaModule
{
    /// <summary>
    /// Route of the schema endpoint relative to the versioned API root.
    /// </summary>
    public const string Route = "/system/schema";



    /// <summary>
    /// The module descriptor: name <c>system</c>, one endpoint.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("system")
        .WithEndpoints(MapSchemaEndpoint);



    /// <summary>
    /// Registers the schema source (the EF-backed one replaces the placeholder in E2) and the module.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsSchemaModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ISchemaVersionSource, NotInstalledSchemaVersionSource>();
        services.AddWmsModule(Descriptor);
        return services;
    }



    private static void MapSchemaEndpoint(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app)
    {
        app.MapGet(Route, async (ISchemaVersionSource source, CancellationToken cancellationToken) =>
                TypedResults.Ok(await source.GetAsync(cancellationToken).ConfigureAwait(false)))
            .WithName("GetSchemaStatus")
            .WithSummary("Current and expected database schema version (read-only).");
    }
}
