// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.DataProtection;
using Wolfgang.Wms.Infrastructure.Database;
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
    /// names: a directory when <c>KeyRingPath</c> is set (created on first run, current user only); else,
    /// when a database provider is configured, the ring in <c>wms.data_protection_key</c> that every
    /// instance shares (E8.6); else (bootstrap without a database) the framework's default. Registers
    /// <see cref="ISecretProtector"/> as the Data Protection implementation unless another is already
    /// registered (E8.5).
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IServiceCollection AddWmsDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new KeyRingOptions();
        configuration.GetSection(KeyRingOptions.SectionName).Bind(options);
        services.AddOptions<KeyRingOptions>().Bind(configuration.GetSection(KeyRingOptions.SectionName));

        var database = new DatabaseOptions();
        configuration.GetSection(DatabaseOptions.SectionName).Bind(database);

        var dataProtection = services.AddDataProtection().SetApplicationName(KeyRing.ApplicationName);
        if (options.UsesFileSystem)
        {
            dataProtection.PersistKeysToFileSystem(KeyRing.EnsureDirectory(options.KeyRingPath!));
        }
        else if (database.ParsedProvider is not (null or DatabaseProvider.None))
        {
            dataProtection.PersistKeysToDbContext<WmsDbContext>();   // E8.6: one ring for every instance, no shared volume
        }

        services.TryAddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        return services;
    }
}
