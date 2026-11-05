// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Http.Paging;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Locations;

/// <summary>
/// The <c>locations</c> module (E17.1): the bins within each site's zones. A location has a code and a barcode
/// unique within the site, a zone, a sortable walk sequence, a pickable flag and an active flag; there is no
/// delete. The list is keyset-paged (<see cref="PageRequest"/>, <see cref="Page{TItem}"/>), sorted by walk
/// sequence by default. Every write is audited through the store (E6.4).
/// </summary>
public static class LocationsModule
{
    /// <summary>See a site's locations.</summary>
    public static readonly Permission Read = new("locations.read", "View the locations") { DefaultRoles = [BuiltInRole.Supervisor, BuiltInRole.Support, BuiltInRole.Viewer] };

    /// <summary>Create, edit and retire locations.</summary>
    public static readonly Permission Write = new("locations.write", "Create, edit and retire locations");



    /// <summary>Route of a site's locations; the <c>siteId</c> route value is the one <see cref="SiteContext"/> reads, so a site-scoped grant applies.</summary>
    public const string Route = "/sites/{siteId:long}/locations";

    /// <summary>Route of one location.</summary>
    public const string LocationRoute = "/sites/{siteId:long}/locations/{locationId:long}";

    /// <summary>The sort field names the list accepts.</summary>
    public const string WalkSequenceSort = "walk_sequence";

    /// <summary>Sort by bin code.</summary>
    public const string CodeSort = "code";

    /// <summary>Sort by barcode.</summary>
    public const string BarcodeSort = "barcode";



    /// <summary>
    /// The sorts the list accepts: walk sequence (the default), code, barcode and id, each ascending or descending.
    /// </summary>
    public static PageSorting Sorting { get; } = new(SortOrder.Ascending(WalkSequenceSort), CodeSort, BarcodeSort, Cursor.IdField);



    /// <summary>
    /// The module descriptor: name <c>locations</c>, the endpoints, the permissions, the error codes.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("locations")
        .WithEndpoints(MapEndpoints)
        .WithPermissions(Read, Write)
        .WithErrorCodes(LocationErrorCodes.SiteNotFound, LocationErrorCodes.NotFound, LocationErrorCodes.ZoneNotFound, LocationErrorCodes.CodeTaken, LocationErrorCodes.BarcodeTaken, LocationErrorCodes.Invalid, LocationErrorCodes.Unavailable);



    /// <summary>
    /// Registers the module, its error handler and the placeholder store (replaced by <c>AddWmsDatabase</c>).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsLocationsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExceptionHandler<LocationExceptionHandler>();
        services.TryAddScoped<ILocations, NoLocations>();
        services.AddWmsModule(Descriptor);
        return services;
    }



    private static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(Route, async (long siteId, [AsParameters] PageRequest page, [FromQuery(Name = "zone_id")] long? zoneId, ILocations locations, CancellationToken cancellationToken) =>
            {
                if (!Required(page).TryResolve(Sorting, out var resolved, out var error))
                {
                    return ApiProblems.Problem(LocationErrorCodes.Invalid, error!, error!);
                }

                return TypedResults.Ok(await locations.ListAsync(siteId, new LocationQuery(zoneId, page.IdFrom, page.IdTo, resolved), cancellationToken).ConfigureAwait(false));
            })
            .RequirePermission(Read)
            .WithName("ListLocations")
            .WithSummary("One page of the site's locations (after/before cursor, sort=walk_sequence|code|barcode|id, size, id_from/id_to, zone_id).")
            .Produces<Page<LocationInfo>>();
        app.MapGet(LocationRoute, async (HttpContext http, long siteId, long locationId, ILocations locations, CancellationToken cancellationToken) =>
                WithEtag(http, await locations.FindAsync(siteId, locationId, cancellationToken).ConfigureAwait(false)))
            .RequirePermission(Read)
            .WithName("GetLocation")
            .WithSummary("One location; the ETag is the row's version.")
            .Produces<LocationInfo>();
        app.MapPost(Route, async (HttpContext http, long siteId, LocationDraft body, ILocations locations, CancellationToken cancellationToken) =>
                WithEtag(http, await locations.CreateAsync(siteId, Required(body), User(http), cancellationToken).ConfigureAwait(false), StatusCodes.Status201Created))
            .RequirePermission(Write)
            .WithName("CreateLocation")
            .WithSummary("Creates a location in the site; 409 locations.code_taken or locations.barcode_taken when in use there.")
            .Produces<LocationInfo>(StatusCodes.Status201Created);
        app.MapPut(LocationRoute, async (HttpContext http, long siteId, long locationId, LocationDraft body, ILocations locations, CancellationToken cancellationToken) =>
            {
                var current = await locations.FindAsync(siteId, locationId, cancellationToken).ConfigureAwait(false);
                return Preconditions.RequireIfMatch(http.Request, EntityTag.FromRowVersion((ulong)current.RowVersion))
                    ?? WithEtag(http, await locations.UpdateAsync(siteId, locationId, Required(body), User(http), cancellationToken).ConfigureAwait(false));
            })
            .RequirePermission(Write)
            .WithName("UpdateLocation")
            .WithSummary("Replaces a location's details; If-Match required.")
            .Produces<LocationInfo>();
    }



    private static T Required<T>(T body)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(body);
        return body;
    }



    private static IResult WithEtag(HttpContext http, LocationInfo location, int statusCode = StatusCodes.Status200OK)
    {
        http.Response.Headers.ETag = location.Etag;
        return statusCode == StatusCodes.Status201Created ? TypedResults.Created(string.Empty, location) : TypedResults.Ok(location);
    }



    private static string User(HttpContext http)
    {
        return http.User.Identity?.Name is { Length: > 0 } name ? name : Settings.SettingsModule.AnonymousUser;
    }
}
