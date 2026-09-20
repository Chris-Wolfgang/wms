// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolfgang.Wms.Core.Modules;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// The <c>settings</c> module (E6): the registry as a service and <c>GET /settings/registry</c>, the one
/// source the console's settings pages and the generated documentation read. Values arrive with E6.3.
/// </summary>
public static class SettingsModule
{
    /// <summary>
    /// Route of the registry endpoint relative to the versioned API root.
    /// </summary>
    public const string RegistryRoute = "/settings/registry";



    /// <summary>
    /// The module descriptor: name <c>settings</c>, the registry endpoint, the module's error codes.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("settings")
        .WithEndpoints(MapRegistryEndpoint)
        .WithErrorCodes(SettingErrorCodes.UnknownKey, SettingErrorCodes.ScopeNotAllowed, SettingErrorCodes.InvalidValue);



    /// <summary>
    /// Registers the module and the registry, which is built from every module registered by the time the
    /// host resolves it, so modules may be added in any order.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsSettingsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(provider => new SettingRegistry(provider.GetRequiredService<ModuleCollection>()));
        services.AddWmsModule(Descriptor);
        return services;
    }



    private static void MapRegistryEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(RegistryRoute, (SettingRegistry registry) => TypedResults.Ok(registry.All.Select(SettingDescriptor.Of).ToList()))
            .WithName("GetSettingRegistry")
            .WithSummary("Every setting the host knows: kind, scopes, default, description (read-only).");
    }
}
