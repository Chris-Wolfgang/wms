// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// How an endpoint declares what it needs (E10.1): a permission, or explicit anonymity. Every endpoint must
/// say one or the other (an architecture test checks); nothing is protected by accident or left open by
/// omission.
/// </summary>
public static class PermissionEndpointExtensions
{
    /// <summary>
    /// Requires <paramref name="permission"/> everywhere or at the request's site (E10.3).
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, Permission permission)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(permission);

        builder.WithMetadata(new PermissionMetadata(permission));
        builder.RequireAuthorization(PermissionPolicyProvider.PolicyName(permission.Name));
        return builder;
    }



    /// <summary>
    /// Registers the catalog, the policy provider and the handler.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsPermissions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthorization();
        services.AddSingleton(provider => new PermissionCatalog(provider.GetRequiredService<Modules.ModuleCollection>()));
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }
}
