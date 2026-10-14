// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Organization;

/// <summary>
/// The <c>organization</c> module (E16.0): the one row per install that names the company, is the top of the
/// settings cascade (the scope formerly called "global"), appears on the console header and the login page,
/// and is the licensee the keys name. Created by the first-run wizard's first step, edited afterwards, never
/// deleted; pickers never see it. Every write is audited through the store (E6.4).
/// </summary>
public static class OrganizationModule
{
    /// <summary>See the organisation's details (address, contacts).</summary>
    public static readonly Permission Read = new("organization.read", "View the organization's details") { DefaultRoles = [BuiltInRole.Supervisor, BuiltInRole.Support, BuiltInRole.Viewer] };

    /// <summary>Create the organisation (first run) and edit it.</summary>
    public static readonly Permission Write = new("organization.write", "Create and edit the organization");



    /// <summary>Route of the organisation.</summary>
    public const string Route = "/organization";

    /// <summary>Route of the anonymous view (name and logo for the login page).</summary>
    public const string PublicRoute = "/organization/public";



    /// <summary>
    /// The module descriptor: name <c>organization</c>, the endpoints, the permissions, the error codes.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("organization")
        .WithEndpoints(MapEndpoints)
        .WithPermissions(Read, Write)
        .WithErrorCodes(OrganizationErrorCodes.NotCreated, OrganizationErrorCodes.AlreadyExists, OrganizationErrorCodes.Invalid, OrganizationErrorCodes.Unavailable);



    /// <summary>
    /// Registers the module, its error handler and the placeholder store (replaced by <c>AddWmsDatabase</c>).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsOrganizationModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExceptionHandler<OrganizationExceptionHandler>();
        services.TryAddScoped<IOrganization, NoOrganization>();
        services.AddWmsModule(Descriptor);
        return services;
    }



    private static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(PublicRoute, async (IOrganization organization, CancellationToken cancellationToken) =>
            {
                var current = await organization.GetAsync(cancellationToken).ConfigureAwait(false);
                return current is null ? ApiProblems.Problem(OrganizationErrorCodes.NotCreated) : TypedResults.Ok(current.ToPublic());
            })
            .AllowAnonymous()   // the login page shows the name and the logo before anyone is signed in
            .WithName("GetOrganizationPublic")
            .WithSummary("The organization's name and logo, for the login page; 404 until the first-run wizard created it.")
            .Produces<OrganizationPublicInfo>();
        app.MapGet(Route, async (HttpContext http, IOrganization organization, CancellationToken cancellationToken) =>
            {
                var current = await organization.GetAsync(cancellationToken).ConfigureAwait(false);
                return current is null ? ApiProblems.Problem(OrganizationErrorCodes.NotCreated) : WithEtag(http, current);
            })
            .RequirePermission(Read)
            .WithName("GetOrganization")
            .WithSummary("The organization; the ETag is the row's version; 404 until the first-run wizard created it.")
            .Produces<OrganizationInfo>();
        app.MapPost(Route, async (HttpContext http, OrganizationDraft body, IOrganization organization, CancellationToken cancellationToken) =>
                WithEtag(http, await organization.CreateAsync(Required(body), User(http), cancellationToken).ConfigureAwait(false), StatusCodes.Status201Created))
            .RequirePermission(Write)
            .WithName("CreateOrganization")
            .WithSummary("Creates the organization (the first-run wizard's first step); 409 when it already exists.")
            .Produces<OrganizationInfo>(StatusCodes.Status201Created);
        app.MapPut(Route, async (HttpContext http, OrganizationDraft body, IOrganization organization, CancellationToken cancellationToken) =>
            {
                var current = await organization.GetAsync(cancellationToken).ConfigureAwait(false);
                if (current is null)
                {
                    return ApiProblems.Problem(OrganizationErrorCodes.NotCreated);
                }

                return Preconditions.RequireIfMatch(http.Request, EntityTag.FromRowVersion((ulong)current.RowVersion))
                    ?? WithEtag(http, await organization.UpdateAsync(Required(body), User(http), cancellationToken).ConfigureAwait(false));
            })
            .RequirePermission(Write)
            .WithName("UpdateOrganization")
            .WithSummary("Replaces the organization's details; If-Match required.")
            .Produces<OrganizationInfo>();
    }



    private static T Required<T>(T body)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(body);
        return body;
    }



    private static IResult WithEtag(HttpContext http, OrganizationInfo organization, int statusCode = StatusCodes.Status200OK)
    {
        http.Response.Headers.ETag = organization.Etag;
        return statusCode == StatusCodes.Status201Created ? TypedResults.Created(string.Empty, organization) : TypedResults.Ok(organization);
    }



    private static string User(HttpContext http)
    {
        return http.User.Identity?.Name is { Length: > 0 } name ? name : Settings.SettingsModule.AnonymousUser;
    }
}
