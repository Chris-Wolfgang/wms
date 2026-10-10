// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// Grants a <see cref="PermissionRequirement"/> when the session carries the permission everywhere or at the
/// request's site (E10.1, E10.3).
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var http = context.Resource as HttpContext;
        var siteId = http is null ? null : SiteContext.SiteIdOf(http);
        if (PermissionClaims.Allows(context.User, requirement.Permission, siteId) || AdmittedSomewhere(context, requirement, http, siteId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }



    /// <summary>
    /// E16.3: a collection endpoint marked <see cref="CollectionScopeMetadata"/> (no <c>siteId</c> in its route)
    /// admits a caller who holds the permission at any site; the endpoint then filters through <see cref="SiteScope"/>.
    /// </summary>
    private static bool AdmittedSomewhere(AuthorizationHandlerContext context, PermissionRequirement requirement, HttpContext? http, long? siteId)
    {
        if (siteId is not null || http?.GetEndpoint()?.Metadata.GetMetadata<CollectionScopeMetadata>() is null)
        {
            return false;
        }

        var scope = SiteScope.Of(context.User, requirement.Permission);
        return scope.IsUnrestricted || scope.SiteIds.Count > 0;
    }
}
