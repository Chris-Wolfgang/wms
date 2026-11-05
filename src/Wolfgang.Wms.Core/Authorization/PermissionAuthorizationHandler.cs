// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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
        var siteId = http is null || IsOrganizationWide(http, requirement) ? null : SiteContext.SiteIdOf(http);
        if (PermissionClaims.Allows(context.User, requirement.Permission, siteId) || AdmittedSomewhere(context, requirement, http))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }



    /// <summary>
    /// E16.3: an endpoint marked <see cref="CollectionScopeMetadata"/> for this permission, with no <c>siteId</c> in
    /// its route, admits a caller who holds the permission at any site, whatever site the <c>X-Wms-Site</c> header
    /// names; the endpoint then filters through <see cref="SiteScope"/>. A routed site keeps its own check, and the
    /// marker relaxes only the permission it names.
    /// </summary>
    private static bool AdmittedSomewhere(AuthorizationHandlerContext context, PermissionRequirement requirement, HttpContext? http)
    {
        if (http is null
            || http.GetRouteValue(SiteContext.RouteValue) is not null
            || http.GetEndpoint()?.Metadata.GetOrderedMetadata<CollectionScopeMetadata>().Any(m => m.Permission.Equals(requirement.Permission)) != true)
        {
            return false;
        }

        var scope = SiteScope.Of(context.User, requirement.Permission);
        return scope.IsUnrestricted || scope.SiteIds.Count > 0;
    }



    private static bool IsOrganizationWide(HttpContext http, PermissionRequirement requirement)
    {
        return http.GetEndpoint()?.Metadata.GetOrderedMetadata<OrganizationScopeMetadata>().Any(m => m.Permission.Equals(requirement.Permission)) == true;
    }
}
