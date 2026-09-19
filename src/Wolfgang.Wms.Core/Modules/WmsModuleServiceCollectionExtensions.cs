// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wolfgang.Wms.Core.Modules;

/// <summary>
/// Service registration for the module system. A module's own <c>AddPickingModule()</c>-style method calls
/// <see cref="AddWmsModule"/> with its descriptor; the host calls nothing else.
/// </summary>
public static class WmsModuleServiceCollectionExtensions
{
    /// <summary>
    /// Ensures the host has exactly one <see cref="ModuleCollection"/> and returns it so modules can be added.
    /// Idempotent: repeated calls return the same collection.
    /// </summary>
    public static ModuleCollection AddWmsModules(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var existing = services
            .FirstOrDefault(d => d.ServiceType == typeof(ModuleCollection))
            ?.ImplementationInstance as ModuleCollection;
        if (existing is not null)
        {
            return existing;
        }

        var collection = new ModuleCollection();
        services.TryAddSingleton(collection);
        return collection;
    }



    /// <summary>
    /// Registers one module's contributions with the host.
    /// </summary>
    public static IServiceCollection AddWmsModule(this IServiceCollection services, ModuleDescriptor module)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(module);

        services.AddWmsModules().Add(module);
        return services;
    }
}
