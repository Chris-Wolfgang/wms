// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Wolfgang.Wms.Core.Modules;

/// <summary>
/// Applies the registered modules to a host.
/// </summary>
public static class WmsModuleEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps every registered module's endpoints, in registration order. Call once after building the app.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <c>AddWmsModules()</c> was never called during service registration.
    /// </exception>
    public static IEndpointRouteBuilder MapWmsModules(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var modules = app.ServiceProvider.GetService<ModuleCollection>()
            ?? throw new InvalidOperationException("No ModuleCollection is registered; call services.AddWmsModules() before building the host.");

        foreach (var module in modules.Modules)
        {
            foreach (var map in module.EndpointMappers)
            {
                map(app);
            }
        }

        return app;
    }
}
