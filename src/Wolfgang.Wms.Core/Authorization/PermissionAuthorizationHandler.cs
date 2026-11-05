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

        var siteId = context.Resource is HttpContext http ? SiteContext.SiteIdOf(http) : null;
        if (PermissionClaims.Allows(context.User, requirement.Permission, siteId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
