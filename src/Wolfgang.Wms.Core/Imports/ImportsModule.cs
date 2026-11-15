// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// The <c>imports</c> module (E16.6): the master data import contract. One file per entity type, the JSON
/// array of the same objects the API takes (plus <c>action</c>), an idempotent upsert by natural key, a
/// bad-row policy per run with a default per entity, and one result shape: counts plus a line per row,
/// downloadable as CSV. The same endpoints serve the console upload, the file drop and the ERP.
/// </summary>
public static class ImportsModule
{
    /// <summary>Load master data files into a site.</summary>
    public static readonly Permission Write = new("imports.write", "Load master data files (zones, locations) into a site");



    /// <summary>Route of the zones import of a site.</summary>
    public const string ZonesRoute = "/sites/{siteId:long}/imports/zones";

    /// <summary>Route of the locations import of a site.</summary>
    public const string LocationsRoute = "/sites/{siteId:long}/imports/locations";

    /// <summary>What an import route ends with to answer with the row results as a CSV download.</summary>
    public const string CsvSuffix = "/csv";

    /// <summary>The media type of the CSV download.</summary>
    public const string CsvContentType = "text/csv";

    /// <summary>The default policy for zones: structural data, so a partial load is refused.</summary>
    public const ImportPolicy ZonesDefaultPolicy = ImportPolicy.AllOrNothing;

    /// <summary>The default policy for locations: independent rows, so the good ones load.</summary>
    public const ImportPolicy LocationsDefaultPolicy = ImportPolicy.AcceptValidRows;



    /// <summary>
    /// The module descriptor: name <c>imports</c>, the endpoints, the permission, the error codes.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("imports")
        .WithEndpoints(MapEndpoints)
        .WithPermissions(Write)
        .WithErrorCodes(ImportErrorCodes.SiteNotFound, ImportErrorCodes.Invalid, ImportErrorCodes.DuplicateInFile, ImportErrorCodes.ReferenceNotFound, ImportErrorCodes.KeyNotFound, ImportErrorCodes.ResolutionZone, ImportErrorCodes.Unavailable);



    /// <summary>
    /// Registers the module, its error handler and the placeholder importer (replaced by <c>AddWmsDatabase</c>).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsImportsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExceptionHandler<ImportExceptionHandler>();
        services.TryAddScoped<IImports, NoImports>();
        services.AddWmsModule(Descriptor);
        return services;
    }



    private static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost(ZonesRoute, async (HttpContext http, long siteId, List<ZoneImportRow> body, [FromQuery(Name = "policy")] string? policy, IImports imports, CancellationToken cancellationToken) =>
                TypedResults.Ok(await ImportZonesAsync(http, siteId, body, policy, imports, cancellationToken).ConfigureAwait(false)))
            .RequirePermission(Write)
            .WithName("ImportZones")
            .WithSummary("Loads a zones file (JSON array of zones plus action) into the site; policy=all_or_nothing (default)|accept_valid_rows|validate_only.")
            .Produces<ImportResult>();
        app.MapPost(ZonesRoute + CsvSuffix, async (HttpContext http, long siteId, List<ZoneImportRow> body, [FromQuery(Name = "policy")] string? policy, IImports imports, CancellationToken cancellationToken) =>
                TypedResults.Text((await ImportZonesAsync(http, siteId, body, policy, imports, cancellationToken).ConfigureAwait(false)).ToCsv(), CsvContentType))
            .RequirePermission(Write)
            .WithName("ImportZonesCsv")
            .WithSummary("The same import, answering with the row results as a downloadable CSV.")
            .Produces<string>(StatusCodes.Status200OK, CsvContentType);
        app.MapPost(LocationsRoute, async (HttpContext http, long siteId, List<LocationImportRow> body, [FromQuery(Name = "policy")] string? policy, IImports imports, CancellationToken cancellationToken) =>
                TypedResults.Ok(await ImportLocationsAsync(http, siteId, body, policy, imports, cancellationToken).ConfigureAwait(false)))
            .RequirePermission(Write)
            .WithName("ImportLocations")
            .WithSummary("Loads a locations file (JSON array of locations with zoneCode plus action) into the site; policy=accept_valid_rows (default)|all_or_nothing|validate_only.")
            .Produces<ImportResult>();
        app.MapPost(LocationsRoute + CsvSuffix, async (HttpContext http, long siteId, List<LocationImportRow> body, [FromQuery(Name = "policy")] string? policy, IImports imports, CancellationToken cancellationToken) =>
                TypedResults.Text((await ImportLocationsAsync(http, siteId, body, policy, imports, cancellationToken).ConfigureAwait(false)).ToCsv(), CsvContentType))
            .RequirePermission(Write)
            .WithName("ImportLocationsCsv")
            .WithSummary("The same import, answering with the row results as a downloadable CSV.")
            .Produces<string>(StatusCodes.Status200OK, CsvContentType);
    }



    private static Task<ImportResult> ImportZonesAsync(HttpContext http, long siteId, List<ZoneImportRow>? body, string? policy, IImports imports, CancellationToken cancellationToken)
    {
        var request = Resolve(body, policy, ZonesDefaultPolicy);
        return imports.ImportZonesAsync(siteId, request.Rows, request.Policy, User(http), cancellationToken);
    }



    private static Task<ImportResult> ImportLocationsAsync(HttpContext http, long siteId, List<LocationImportRow>? body, string? policy, IImports imports, CancellationToken cancellationToken)
    {
        var request = Resolve(body, policy, LocationsDefaultPolicy);
        return imports.ImportLocationsAsync(siteId, request.Rows, request.Policy, User(http), cancellationToken);
    }



    private static (IReadOnlyList<T> Rows, ImportPolicy Policy) Resolve<T>(List<T>? body, string? policyText, ImportPolicy fallback)
    {
        var rows = body ?? [];
        if (ImportRules.ValidateSize(rows.Count) is { } sizeReason)
        {
            throw new ImportException(ImportErrorCodes.Invalid, sizeReason);
        }

        var policy = ImportRules.ParsePolicy(policyText, fallback)
            ?? throw new ImportException(ImportErrorCodes.Invalid, $"policy must be {ImportRules.AllOrNothing}, {ImportRules.AcceptValidRows} or {ImportRules.ValidateOnly}.");
        return (rows, policy);
    }



    private static string User(HttpContext http)
    {
        return http.User.Identity?.Name is { Length: > 0 } name ? name : Settings.SettingsModule.AnonymousUser;
    }
}
