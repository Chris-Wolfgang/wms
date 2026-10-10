// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Core.Zones;

namespace Wolfgang.Wms.Core.Copies;

/// <summary>
/// The <c>copies</c> module (E16.5): copy a site, a zone, a location or a range of locations to set up a
/// similar one without re-entering everything. Every copy is an API operation (usable by the console and by
/// CSV/API clients alike), audited as creates that carry the <c>copiedFromId</c> of their source, and never
/// brings transactional data or pickers' assignments. The permissions are the entities' own write permissions.
/// </summary>
public static class CopiesModule
{
    /// <summary>Route of the site copy.</summary>
    public const string SiteRoute = "/sites/{siteId:long}/copy";

    /// <summary>Route of the zone copy.</summary>
    public const string ZoneRoute = "/sites/{siteId:long}/zones/{zoneId:long}/copy";

    /// <summary>Route of the location copy.</summary>
    public const string LocationRoute = "/sites/{siteId:long}/locations/{locationId:long}/copy";

    /// <summary>Route of the location range copy.</summary>
    public const string LocationRangeRoute = "/sites/{siteId:long}/locations/copy-range";



    /// <summary>
    /// The module descriptor: name <c>copies</c>, the endpoints, the error codes; no permissions of its own.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("copies")
        .WithEndpoints(MapEndpoints)
        .WithErrorCodes(CopyErrorCodes.NotFound, CopyErrorCodes.Invalid, CopyErrorCodes.CodeTaken, CopyErrorCodes.BarcodeTaken, CopyErrorCodes.Unavailable);



    /// <summary>
    /// Registers the module, its error handler and the placeholder copier (replaced by <c>AddWmsDatabase</c>).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsCopiesModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExceptionHandler<CopyExceptionHandler>();
        services.TryAddScoped<ICopies, NoCopies>();
        services.AddWmsModule(Descriptor);
        return services;
    }



    private static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost(SiteRoute, async (HttpContext http, long siteId, SiteCopyRequest body, ICopies copies, CancellationToken cancellationToken) =>
                Created(http, await copies.CopySiteAsync(siteId, Required(body), User(http), cancellationToken).ConfigureAwait(false)))
            .RequirePermission(SitesModule.Write)
            .WithName("CopySite")
            .WithSummary("Copies the site (settings overrides, zones, locations as chosen) under a new code; the copy carries copiedFromId.")
            .Produces<SiteInfo>(StatusCodes.Status201Created);
        app.MapPost(ZoneRoute, async (HttpContext http, long siteId, long zoneId, ZoneCopyRequest body, ICopies copies, CancellationToken cancellationToken) =>
                Created(http, await copies.CopyZoneAsync(siteId, zoneId, Required(body), User(http), cancellationToken).ConfigureAwait(false)))
            .RequirePermission(ZonesModule.Write)
            .WithName("CopyZone")
            .WithSummary("Copies the zone within or across sites (settings overrides, locations with a code-prefix substitution as chosen).")
            .Produces<ZoneInfo>(StatusCodes.Status201Created);
        app.MapPost(LocationRoute, async (HttpContext http, long siteId, long locationId, LocationCopyRequest body, ICopies copies, CancellationToken cancellationToken) =>
                Created(http, await copies.CopyLocationAsync(siteId, locationId, Required(body), User(http), cancellationToken).ConfigureAwait(false)))
            .RequirePermission(LocationsModule.Write)
            .WithName("CopyLocation")
            .WithSummary("Copies the location with a new code and barcode.")
            .Produces<LocationInfo>(StatusCodes.Status201Created);
        app.MapPost(LocationRangeRoute, async (HttpContext http, long siteId, LocationRangeCopyRequest body, ICopies copies, CancellationToken cancellationToken) =>
                TypedResults.Created(string.Empty, await copies.CopyLocationRangeAsync(siteId, Required(body), User(http), cancellationToken).ConfigureAwait(false)))
            .RequirePermission(LocationsModule.Write)
            .WithName("CopyLocationRange")
            .WithSummary("Copies every location whose code starts with a prefix, substituting the prefix in codes and barcodes; all or nothing.")
            .Produces<IReadOnlyList<LocationInfo>>(StatusCodes.Status201Created);
    }



    private static T Required<T>(T body)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(body);
        return body;
    }



    private static IResult Created<T>(HttpContext http, T created)
        where T : class
    {
        http.Response.Headers.ETag = created switch
        {
            SiteInfo site => site.Etag,
            ZoneInfo zone => zone.Etag,
            LocationInfo location => location.Etag,
            _ => http.Response.Headers.ETag,
        };
        return TypedResults.Created(string.Empty, created);
    }



    private static string User(HttpContext http)
    {
        return http.User.Identity?.Name is { Length: > 0 } name ? name : Settings.SettingsModule.AnonymousUser;
    }
}
