// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wolfgang.Wms.Core.Devices;

/// <summary>
/// Wiring for device version enforcement (E82.7).
/// </summary>
public static class DeviceVersionConventions
{
    /// <summary>
    /// Registers <see cref="NoMinimumDeviceVersionPolicy"/> as the fallback when no <see cref="IDeviceVersionPolicy"/>
    /// is registered yet. Call it after the modules: the settings module (E12) registers the organisation → site
    /// cascade, and a policy registered earlier is kept.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsDeviceVersioning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDeviceVersionPolicy, NoMinimumDeviceVersionPolicy>();
        return services;
    }



    /// <summary>
    /// Requires every request to the endpoint (or group) to carry the device app version and to meet the
    /// site's minimum; apply to every device-facing group.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static TBuilder RequireDeviceVersion<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter<TBuilder, DeviceVersionFilter>();
    }
}
