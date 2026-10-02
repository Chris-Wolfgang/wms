// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolfgang.Wms.Core.Secrets;

namespace Wolfgang.Wms.Infrastructure.Secrets;

/// <summary>
/// Registers Data Protection and the secret protector (E8.1, E8.5).
/// </summary>
public static class SecretsServiceCollectionExtensions
{
    /// <summary>
    /// Adds Data Protection under the shared application name with the ring <c>Wms:DataProtection</c>
    /// names: a directory when <c>KeyRingPath</c> is set (created on first run, current user only), else the
    /// database ring registered by <c>AddWmsDatabase</c> (E8.6). Registers <see cref="ISecretProtector"/> as
    /// the Data Protection implementation unless another is already registered (E8.5).
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IServiceCollection AddWmsDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new KeyRingOptions();
        configuration.GetSection(KeyRingOptions.SectionName).Bind(options);
        services.AddOptions<KeyRingOptions>().Bind(configuration.GetSection(KeyRingOptions.SectionName));

        var dataProtection = services.AddDataProtection().SetApplicationName(KeyRing.ApplicationName);
        if (options.UsesFileSystem)
        {
            dataProtection.PersistKeysToFileSystem(KeyRing.EnsureDirectory(options.KeyRingPath!));
        }

        services.TryAddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        return services;
    }
}
