// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Identity.Providers;
using Wolfgang.Wms.Core.Modules;

namespace Wolfgang.Wms.Auth.Oidc;

/// <summary>
/// Registers the <c>oidc</c> provider (E11.1): the provider, the handler's options plumbing (built from
/// settings, never from <c>appsettings</c>), the handler itself, and a module declaring the settings.
/// The scheme is added at runtime when <c>auth.providers.enabled</c> names <c>oidc</c>.
/// </summary>
public static class OidcServiceCollectionExtensions
{
    /// <summary>
    /// The module descriptor: <c>oidc</c>, settings only.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("oidc")
        .WithSettings(OidcSettings.All);



    /// <summary>
    /// Adds the OIDC provider. Call after <c>AddWmsAuthModule</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsOidcProvider(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!services.Any(d => d.ServiceType == typeof(OidcAuthProvider)))
        {
            services.AddSingleton<OidcAuthProvider>();
            services.AddSingleton<IAuthProvider>(provider => provider.GetRequiredService<OidcAuthProvider>());   // one instance behind both
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<OpenIdConnectOptions>, OidcOptionsConfigurator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<OpenIdConnectOptions>, OpenIdConnectPostConfigureOptions>());   // discovery, backchannel, state protection
        services.TryAddTransient<OpenIdConnectHandler>();
        services.AddHttpClient(OidcAuthProvider.DiscoveryClient, client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddWmsModule(Descriptor);
        return services;
    }
}
