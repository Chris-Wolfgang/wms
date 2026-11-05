// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wolfgang.Wms.Infrastructure.Integrity;

/// <summary>
/// Registers the signer and the interceptor (with the database) and the verification job (in the worker).
/// </summary>
public static class IntegrityServiceCollectionExtensions
{
    /// <summary>
    /// The signer (singleton; the key is loaded on first use) and the save interceptor that signs rows.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsIntegritySigning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IntegritySigner>();
        services.TryAddSingleton<IIntegritySigner>(provider => provider.GetRequiredService<IntegritySigner>());
        services.TryAddSingleton<IntegritySigningInterceptor>();
        return services;
    }



    /// <summary>
    /// The scheduled verification of every signed row (E10.4); one host per installation runs it.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsIntegrityVerification(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IntegrityVerificationJob>();
        services.AddHostedService(provider => provider.GetRequiredService<IntegrityVerificationJob>());
        return services;
    }
}
