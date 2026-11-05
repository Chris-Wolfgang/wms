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

namespace Wolfgang.Wms.Core.Zones;

/// <summary>
/// The <c>zones</c> module (E16.2): the picking areas, bulk storage and resolution zones within each site. A
/// zone has a code unique within its site, a name, a type, an optional walk-order prefix, the reject-lane flag
/// (pick zones) and, for resolution zones, the properties ordinary zones do not have. Resolution zones are
/// created only here, never by ERP or CSV load. Retiring a zone is refused while zone groups are open in it;
/// there is no delete. Every write is audited through the store (E6.4).
/// </summary>
public static class ZonesModule
{
    /// <summary>See a site's zones.</summary>
    public static readonly Permission Read = new("zones.read", "View the zones") { DefaultRoles = [BuiltInRole.Supervisor, BuiltInRole.Support, BuiltInRole.Viewer] };

    /// <summary>Create, edit and retire zones.</summary>
    public static readonly Permission Write = new("zones.write", "Create, edit and retire zones");



    /// <summary>Route of a site's zones; the <c>siteId</c> route value is the one <see cref="SiteContext"/> reads, so a site-scoped grant applies.</summary>
    public const string Route = "/sites/{siteId:long}/zones";

    /// <summary>Route of one zone.</summary>
    public const string ZoneRoute = "/sites/{siteId:long}/zones/{zoneId:long}";



    /// <summary>
    /// The module descriptor: name <c>zones</c>, the endpoints, the permissions, the error codes.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("zones")
        .WithEndpoints(MapEndpoints)
        .WithPermissions(Read, Write)
        .WithErrorCodes(ZoneErrorCodes.SiteNotFound, ZoneErrorCodes.NotFound, ZoneErrorCodes.CodeTaken, ZoneErrorCodes.Invalid, ZoneErrorCodes.HasOpenGroups, ZoneErrorCodes.Unavailable);



    /// <summary>
    /// Registers the module, its error handler, the placeholder store (replaced by <c>AddWmsDatabase</c>) and the
    /// open-groups answer that never blocks (replaced by tote entry, E26).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsZonesModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExceptionHandler<ZoneExceptionHandler>();
        services.TryAddScoped<IZones, NoZones>();
        services.TryAddSingleton<IOpenZoneGroups, NoOpenZoneGroups>();
        services.AddWmsModule(Descriptor);
        return services;
    }



    private static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(Route, async (long siteId, IZones zones, CancellationToken cancellationToken) => TypedResults.Ok(await zones.ListAsync(siteId, cancellationToken).ConfigureAwait(false)))
            .RequirePermission(Read)
            .WithName("ListZones")
            .WithSummary("Every zone of the site, active and retired, in code order.")
            .Produces<IReadOnlyList<ZoneInfo>>();
        app.MapGet(ZoneRoute, async (HttpContext http, long siteId, long zoneId, IZones zones, CancellationToken cancellationToken) =>
                WithEtag(http, await zones.FindAsync(siteId, zoneId, cancellationToken).ConfigureAwait(false)))
            .RequirePermission(Read)
            .WithName("GetZone")
            .WithSummary("One zone; the ETag is the row's version.")
            .Produces<ZoneInfo>();
        app.MapPost(Route, async (HttpContext http, long siteId, ZoneDraft body, IZones zones, CancellationToken cancellationToken) =>
                WithEtag(http, await zones.CreateAsync(siteId, Required(body), User(http), cancellationToken).ConfigureAwait(false), StatusCodes.Status201Created))
            .RequirePermission(Write)
            .WithName("CreateZone")
            .WithSummary("Creates a zone in the site; 409 zones.code_taken when the code is in use there.")
            .Produces<ZoneInfo>(StatusCodes.Status201Created);
        app.MapPut(ZoneRoute, async (HttpContext http, long siteId, long zoneId, ZoneDraft body, IZones zones, CancellationToken cancellationToken) =>
            {
                var current = await zones.FindAsync(siteId, zoneId, cancellationToken).ConfigureAwait(false);
                return Preconditions.RequireIfMatch(http.Request, EntityTag.FromRowVersion((ulong)current.RowVersion))
                    ?? WithEtag(http, await zones.UpdateAsync(siteId, zoneId, Required(body), User(http), cancellationToken).ConfigureAwait(false));
            })
            .RequirePermission(Write)
            .WithName("UpdateZone")
            .WithSummary("Replaces a zone's details; If-Match required; 409 zones.has_open_groups when retiring a zone with open groups.")
            .Produces<ZoneInfo>();
    }



    private static T Required<T>(T body)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(body);
        return body;
    }



    private static IResult WithEtag(HttpContext http, ZoneInfo zone, int statusCode = StatusCodes.Status200OK)
    {
        http.Response.Headers.ETag = zone.Etag;
        return statusCode == StatusCodes.Status201Created ? TypedResults.Created(string.Empty, zone) : TypedResults.Ok(zone);
    }



    private static string User(HttpContext http)
    {
        return http.User.Identity?.Name is { Length: > 0 } name ? name : Settings.SettingsModule.AnonymousUser;
    }
}
