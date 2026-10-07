// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;

/// <summary>
/// Registers the host-only admin channel (E9.3). Call after <c>AddWmsAuthModule</c> and
/// <c>AddWmsDataProtection</c>: the server seals and opens requests with the host's ring and drives the
/// gate the auth module registered.
/// </summary>
public static class AdminChannelServiceCollectionExtensions
{
    /// <summary>
    /// Adds the channel: options from <c>Wms:Admin</c> (read directly, no binder), the handler, the server.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IServiceCollection AddWmsAdminChannel(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var name = configuration[AdminChannelOptions.NameKey];
        var enabled = !bool.TryParse(configuration[AdminChannelOptions.EnabledKey], out var parsed) || parsed;
        services.Configure<AdminChannelOptions>(options =>
        {
            options.ChannelName = string.IsNullOrWhiteSpace(name) ? AdminChannelProtocol.DefaultName : name.Trim();
            options.Enabled = enabled;
        });
        services.TryAddSingleton<AdminChannelHandler>();
        services.AddHostedService<AdminChannelServer>();
        return services;
    }
}
