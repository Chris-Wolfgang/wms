// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The <c>license</c> module (E79): the license in force, key install and removal, the comparison. The
/// free tier is compiled in; a paid key is pasted, verified offline against the vendor's public key, stored
/// encrypted as a setting and in force on every instance within seconds.
/// </summary>
public static class LicenseModule
{
    /// <summary>Route of the license page.</summary>
    public const string Route = "/system/license";

    /// <summary>Route of the installed keys.</summary>
    public const string KeysRoute = "/system/license/keys";

    /// <summary>Route of one installed key.</summary>
    public const string KeyRoute = "/system/license/keys/{keyId}";

    /// <summary>Route of the feature comparison.</summary>
    public const string ComparisonRoute = "/system/license/comparison";



    /// <summary>
    /// The module descriptor: name <c>license</c>, the endpoints, the settings, the permissions, every
    /// license feature and the error codes.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("license")
        .WithEndpoints(MapEndpoints)
        .WithSettings(LicenseSettings.All)
        .WithPermissions(LicensePermissions.All)
        .WithLicenseFeatures(LicenseFeatures.All)
        .WithErrorCodes(LicenseErrorCodes.All);



    /// <summary>
    /// Registers the verifier (the vendor's key), the state, the sync, the gate, the usage placeholder and
    /// the module. A test registers its own <see cref="LicenseVerifier"/> first to issue keys with its own pair.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsLicenseModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(_ => LicenseVerifier.ForVendor());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<LicenseState>();
        services.TryAddSingleton<ILicense>(provider => provider.GetRequiredService<LicenseState>());
        services.TryAddScoped<ILicenseUsage, NoLicenseUsage>();
        services.TryAddScoped<LicenseGate>();
        services.AddHostedService<LicenseSync>();
        services.AddExceptionHandler<LicenseExceptionHandler>();
        services.AddWmsModule(Descriptor);
        return services;
    }



    private static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(Route, LicenseEndpoints.StatusAsync)
            .RequirePermission(LicensePermissions.Read)
            .WithName("GetLicense")
            .WithSummary("The license in force: tier, coverage, features, limits with usage, installed keys, banners (E79.6).")
            .Produces<LicenseStatus>(StatusCodes.Status200OK);
        app.MapPut(KeysRoute, LicenseEndpoints.InstallAsync)
            .RequirePermission(LicensePermissions.Manage)
            .WithName("InstallLicenseKey")
            .WithSummary("Installs a signed key (verified offline); a key with the same id replaces the old one; in force at once (E79.3, E79.11).")
            .Produces<LicenseStatus>(StatusCodes.Status200OK);
        app.MapDelete(KeyRoute, LicenseEndpoints.RemoveAsync)
            .RequirePermission(LicensePermissions.Manage)
            .WithName("RemoveLicenseKey")
            .WithSummary("Removes an installed key by id.")
            .Produces<LicenseStatus>(StatusCodes.Status200OK);
        app.MapGet(ComparisonRoute, LicenseEndpoints.Comparison)
            .RequirePermission(LicensePermissions.Read)
            .WithName("GetLicenseComparison")
            .WithSummary("The feature-by-tier table of this release, with the installed tier marked (E79.10).")
            .Produces<FeatureComparison>(StatusCodes.Status200OK);
    }
}
