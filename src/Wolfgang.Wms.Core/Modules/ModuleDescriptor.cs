// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Routing;

namespace Wolfgang.Wms.Core.Modules;

/// <summary>
/// Everything a module contributes to the host, declared once by the module's <c>Add…Module()</c> method
/// and applied by <see cref="WmsModuleEndpointRouteBuilderExtensions.MapWmsModules"/>. Registration is
/// explicit: there is no assembly scanning.
/// </summary>
/// <remarks>
/// Endpoints are the first contribution kind. Jobs, settings, permissions, issue types, navigation, EF
/// configurations, resources and error codes join this record as their key types arrive (E1.13, E12, E6,
/// E10); each is a typed list, never a string.
/// </remarks>
/// <param name="Name">Stable module name, unique within a host, for example <c>Picking</c>.</param>
/// <param name="EndpointMappers">Callbacks that map the module's endpoints onto the host's route builder, in order.</param>
public sealed record ModuleDescriptor
(
    string Name,
    IReadOnlyList<Action<IEndpointRouteBuilder>> EndpointMappers
)
{
    /// <summary>
    /// A descriptor with no contributions yet; add them with the fluent <c>With…</c> methods.
    /// </summary>
    public static ModuleDescriptor Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new ModuleDescriptor(name, []);
    }



    /// <summary>
    /// Adds an endpoint mapper. Mappers run in the order they were added, once, when the host maps modules.
    /// </summary>
    public ModuleDescriptor WithEndpoints(Action<IEndpointRouteBuilder> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return this with { EndpointMappers = [.. EndpointMappers, map] };
    }
}
