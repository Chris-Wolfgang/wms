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

namespace Wolfgang.Wms.Core.Sites;

/// <summary>
/// The <c>sites</c> module (E16.1): the warehouses the install runs. A site has a unique code, a name, a time
/// zone and an active flag; it is the operational level pickers, role assignments (<c>siteId</c>, <c>X-Wms-Site</c>)
/// and the settings cascade are scoped to. Retiring a site is refused while releases are open against it; there
/// is no delete. Every write is audited through the store (E6.4).
/// </summary>
public static class SitesModule
{
    /// <summary>See the sites.</summary>
    public static readonly Permission Read = new("sites.read", "View the sites") { DefaultRoles = [BuiltInRole.Supervisor, BuiltInRole.Support, BuiltInRole.Viewer] };

    /// <summary>Create, edit and retire sites.</summary>
    public static readonly Permission Write = new("sites.write", "Create, edit and retire sites");



    /// <summary>Route of the collection.</summary>
    public const string Route = "/sites";

    /// <summary>Route of one site; the <c>siteId</c> route value is the one <see cref="SiteContext"/> reads, so a site-scoped grant applies.</summary>
    public const string SiteRoute = "/sites/{siteId:long}";



    /// <summary>
    /// The module descriptor: name <c>sites</c>, the endpoints, the permissions, the error codes.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("sites")
        .WithEndpoints(MapEndpoints)
        .WithPermissions(Read, Write)
        .WithErrorCodes(SiteErrorCodes.NotFound, SiteErrorCodes.CodeTaken, SiteErrorCodes.Invalid, SiteErrorCodes.HasOpenReleases, SiteErrorCodes.Unavailable);



    /// <summary>
    /// Registers the module, its error handler, the placeholder store (replaced by <c>AddWmsDatabase</c>) and the
    /// open-releases answer that never blocks (replaced by release intake, E22).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsSitesModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExceptionHandler<SiteExceptionHandler>();
        services.TryAddScoped<ISites, NoSites>();
        services.TryAddSingleton<IOpenReleases, NoOpenReleases>();
        services.AddWmsModule(Descriptor);
        return services;
    }



    private static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(Route, async (HttpContext http, ISites sites, CancellationToken cancellationToken) => TypedResults.Ok(await sites.ListAsync(SiteScope.Of(http.User, Read), cancellationToken).ConfigureAwait(false)))
            .RequirePermissionInScope(Read)
            .WithName("ListSites")
            .WithSummary("Every site the caller may see (all with an organization-level grant, their own with site-level grants), active and retired, in code order.")
            .Produces<IReadOnlyList<SiteInfo>>();
        app.MapGet(SiteRoute, async (HttpContext http, long siteId, ISites sites, CancellationToken cancellationToken) =>
                WithEtag(http, await sites.FindAsync(siteId, cancellationToken).ConfigureAwait(false)))
            .RequirePermission(Read)
            .WithName("GetSite")
            .WithSummary("One site; the ETag is the row's version.")
            .Produces<SiteInfo>();
        app.MapPost(Route, async (HttpContext http, SiteDraft body, ISites sites, CancellationToken cancellationToken) =>
                WithEtag(http, await sites.CreateAsync(Required(body), User(http), cancellationToken).ConfigureAwait(false), StatusCodes.Status201Created))
            .RequirePermission(Write)
            .WithName("CreateSite")
            .WithSummary("Creates a site; 409 sites.code_taken when the code is in use.")
            .Produces<SiteInfo>(StatusCodes.Status201Created);
        app.MapPut(SiteRoute, async (HttpContext http, long siteId, SiteDraft body, ISites sites, CancellationToken cancellationToken) =>
            {
                var current = await sites.FindAsync(siteId, cancellationToken).ConfigureAwait(false);
                return Preconditions.RequireIfMatch(http.Request, EntityTag.FromRowVersion((ulong)current.RowVersion))
                    ?? WithEtag(http, await sites.UpdateAsync(siteId, Required(body), User(http), cancellationToken).ConfigureAwait(false));
            })
            .RequirePermission(Write)
            .WithName("UpdateSite")
            .WithSummary("Replaces a site's details; If-Match required; 409 sites.has_open_releases when retiring a site with open releases.")
            .Produces<SiteInfo>();
    }



    private static T Required<T>(T body)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(body);
        return body;
    }



    private static IResult WithEtag(HttpContext http, SiteInfo site, int statusCode = StatusCodes.Status200OK)
    {
        http.Response.Headers.ETag = site.Etag;
        return statusCode == StatusCodes.Status201Created ? TypedResults.Created(string.Empty, site) : TypedResults.Ok(site);
    }



    private static string User(HttpContext http)
    {
        return http.User.Identity?.Name is { Length: > 0 } name ? name : Settings.SettingsModule.AnonymousUser;
    }
}
