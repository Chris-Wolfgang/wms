// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// The <c>roles</c> module (E10.2, E10.3): roles built from the catalog, editable copies of the built-in
/// ones, and assignments per user everywhere or per site with an optional expiry. Every write is audited
/// through the store (E6.4).
/// </summary>
public static class RolesModule
{
    /// <summary>See roles and assignments.</summary>
    public static readonly Permission Read = new("auth.roles.read", "View roles and who holds them") { DefaultRoles = [BuiltInRole.Support] };

    /// <summary>Create, edit, copy and delete roles; assign and unassign them.</summary>
    public static readonly Permission Write = new("auth.roles.write", "Manage roles and assignments");



    /// <summary>Route of the role collection.</summary>
    public const string RolesRoute = "/auth/roles";

    /// <summary>Route of one role.</summary>
    public const string RoleRoute = "/auth/roles/{roleId:long}";

    /// <summary>Route of a user's assignments.</summary>
    public const string UserRolesRoute = "/auth/users/{userId:long}/roles";

    /// <summary>Route of one assignment.</summary>
    public const string AssignmentRoute = "/auth/assignments/{assignmentId:long}";



    /// <summary>
    /// The module descriptor: name <c>roles</c>, the endpoints, the permissions, the error codes.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("roles")
        .WithEndpoints(MapRoleEndpoints)
        .WithEndpoints(MapAssignmentEndpoints)
        .WithPermissions(Read, Write)
        .WithErrorCodes(RoleErrorCodes.RoleNotFound, RoleErrorCodes.RoleNameTaken, RoleErrorCodes.BuiltInRoleReadOnly, RoleErrorCodes.UnknownPermission, RoleErrorCodes.AssignmentNotFound, RoleErrorCodes.UserNotFound, RoleErrorCodes.InvalidRole);



    /// <summary>
    /// Registers the module and the placeholder store (replaced by <c>AddWmsDatabase</c>).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsRolesModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IRoles, NoRoles>();
        services.AddWmsModule(Descriptor);
        return services;
    }



    private static void MapRoleEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(RolesRoute, async (IRoles roles, CancellationToken cancellationToken) => TypedResults.Ok(await roles.ListAsync(cancellationToken).ConfigureAwait(false)))
            .RequirePermission(Read)
            .WithName("ListRoles")
            .WithSummary("Every role: built-in first, then by name.");
        app.MapGet(RoleRoute, async (HttpContext http, long roleId, IRoles roles, CancellationToken cancellationToken) => WithEtag(http, await roles.FindAsync(roleId, cancellationToken).ConfigureAwait(false)))
            .RequirePermission(Read)
            .WithName("GetRole")
            .WithSummary("One role; the ETag is the row's version.");
        app.MapPost(RolesRoute, async (HttpContext http, RoleDraft body, IRoles roles, CancellationToken cancellationToken) =>
                WithEtag(http, await roles.CreateAsync(Required(body), User(http), cancellationToken).ConfigureAwait(false), StatusCodes.Status201Created))
            .RequirePermission(Write)
            .WithName("CreateRole")
            .WithSummary("Creates a role from catalog permissions.")
            .Produces<RoleInfo>(StatusCodes.Status201Created);
        app.MapPut(RoleRoute, async (HttpContext http, long roleId, RoleDraft body, IRoles roles, CancellationToken cancellationToken) =>
            {
                var current = await roles.FindAsync(roleId, cancellationToken).ConfigureAwait(false);
                return Preconditions.RequireIfMatch(http.Request, EntityTag.FromRowVersion((ulong)current.RowVersion))
                    ?? WithEtag(http, await roles.UpdateAsync(roleId, Required(body), User(http), cancellationToken).ConfigureAwait(false));
            })
            .RequirePermission(Write)
            .WithName("UpdateRole")
            .WithSummary("Replaces a role's name, description and permissions; If-Match required; built-in roles are read-only.")
            .Produces<RoleInfo>();
        app.MapPost(RoleRoute + "/copy", async (HttpContext http, long roleId, CopyRoleRequest body, IRoles roles, CancellationToken cancellationToken) =>
                WithEtag(http, await roles.CopyAsync(roleId, Required(body).Name, User(http), cancellationToken).ConfigureAwait(false), StatusCodes.Status201Created))
            .RequirePermission(Write)
            .WithName("CopyRole")
            .WithSummary("Creates an editable copy of a role (the way to customise a built-in one).")
            .Produces<RoleInfo>(StatusCodes.Status201Created);
        app.MapDelete(RoleRoute, async (HttpContext http, long roleId, IRoles roles, CancellationToken cancellationToken) =>
            {
                await roles.DeleteAsync(roleId, User(http), cancellationToken).ConfigureAwait(false);
                return TypedResults.NoContent();
            })
            .RequirePermission(Write)
            .WithName("DeleteRole")
            .WithSummary("Deletes a role and its assignments; built-in roles cannot be deleted.");
    }



    private static void MapAssignmentEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(UserRolesRoute, async (long userId, IRoles roles, CancellationToken cancellationToken) => TypedResults.Ok(await roles.AssignmentsOfAsync(userId, cancellationToken).ConfigureAwait(false)))
            .RequirePermission(Read)
            .WithName("ListUserRoles")
            .WithSummary("A user's role assignments, active and expired.");
        app.MapPost(UserRolesRoute, async (HttpContext http, long userId, AssignRoleRequest body, IRoles roles, CancellationToken cancellationToken) =>
                TypedResults.Created(string.Empty, await roles.AssignAsync(userId, Required(body).RoleId, body.SiteId, body.ExpiresAt, User(http), cancellationToken).ConfigureAwait(false)))
            .RequirePermission(Write)
            .WithName("AssignRole")
            .WithSummary("Assigns a role everywhere (no site) or at one site, optionally until a date.")
            .Produces<RoleAssignmentInfo>(StatusCodes.Status201Created);
        app.MapDelete(AssignmentRoute, async (HttpContext http, long assignmentId, IRoles roles, CancellationToken cancellationToken) =>
            {
                await roles.UnassignAsync(assignmentId, User(http), cancellationToken).ConfigureAwait(false);
                return TypedResults.NoContent();
            })
            .RequirePermission(Write)
            .WithName("UnassignRole")
            .WithSummary("Removes an assignment.");
    }



    private static T Required<T>(T body)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(body);
        return body;
    }



    private static IResult WithEtag(HttpContext http, RoleInfo role, int statusCode = StatusCodes.Status200OK)
    {
        http.Response.Headers.ETag = role.Etag;
        return statusCode == StatusCodes.Status201Created ? TypedResults.Created(string.Empty, role) : TypedResults.Ok(role);
    }



    private static string User(HttpContext http)
    {
        return http.User.Identity?.Name is { Length: > 0 } name ? name : Settings.SettingsModule.AnonymousUser;
    }
}
