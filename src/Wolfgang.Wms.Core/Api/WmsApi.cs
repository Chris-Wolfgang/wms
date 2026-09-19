// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Wolfgang.Wms.Core.Api;

/// <summary>
/// The one web API (E82): path-segment versioned under <c>/api/v{n}/</c>, one OpenAPI document per version.
/// <c>v0</c> is the unstable contract for product 0.x (breaking changes in place, each with a <c>breaking</c>
/// fragment); <c>v1</c> freezes at product 1.0 and <see cref="Frozen"/> is what the CI OpenAPI diff protects.
/// The console (E82.4) and every customer tool are clients of this API; nothing bypasses it (E82.1).
/// </summary>
/// <remarks>
/// Minimal APIs only: the version-aware API explorer of <c>Asp.Versioning.Mvc.ApiExplorer</c> is MVC-based and
/// not trim-safe (E1.9), so each document filters endpoints by their <see cref="ApiVersionMetadata"/> and a
/// transformer substitutes the version into the paths itself.
/// </remarks>
public static class WmsApi
{
    /// <summary>
    /// Route template for every API endpoint; the version is a path segment, never a header or query string.
    /// </summary>
    public const string RouteTemplate = "/api/v{version:apiVersion}";



    /// <summary>
    /// The unstable contract shipped through product 0.x.
    /// </summary>
    public static ApiVersion V0 { get; } = new(0);



    /// <summary>
    /// The version new endpoints are added to.
    /// </summary>
    public static ApiVersion Current => V0;



    /// <summary>
    /// Every version served by the API, oldest first. Each has its own OpenAPI document (<c>/openapi/v{n}.json</c>).
    /// </summary>
    public static IReadOnlyList<ApiVersion> Served { get; } = [V0];



    /// <summary>
    /// Versions whose contract may no longer break: none until product 1.0 freezes <c>v1</c>. A frozen version
    /// changes only additively; removal of a frozen version is a product major.
    /// </summary>
    public static IReadOnlyList<ApiVersion> Frozen { get; } = [];



    /// <summary>
    /// Registers path-segment API versioning (unspecified version is an error, versions reported in
    /// <c>api-supported-versions</c>) and one OpenAPI document per served version that contains only the
    /// endpoints mapped to that version, with the version substituted into every path.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsApiVersioning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = V0;
            options.AssumeDefaultVersionWhenUnspecified = false;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });
        services.AddEndpointsApiExplorer();

        foreach (var version in Served)
        {
            services.AddOpenApi(DocumentName(version), options => ConfigureDocument(options, version));
        }

        return services;
    }



    /// <summary>
    /// Maps the versioned API root (<see cref="RouteTemplate"/>) that every module maps its endpoints under,
    /// and the OpenAPI documents at <c>/openapi/v{n}.json</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static RouteGroupBuilder MapWmsApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var api = app.NewVersionedApi("wms");
        var group = api.MapGroup(RouteTemplate);
        foreach (var version in Served)
        {
            group.HasApiVersion(version);
        }

        app.MapOpenApi();
        return group;
    }



    /// <summary>
    /// OpenAPI document name of a version: <c>v0</c>, <c>v1</c>.
    /// </summary>
    public static string DocumentName(ApiVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return "v" + version.MajorVersion;
    }



    /// <summary>
    /// Path with the version substituted (<c>/api/v{version}/skus</c> → <c>/api/v0/skus</c>).
    /// </summary>
    public static string SubstituteVersion(string path, ApiVersion version)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(version);

        return path.Replace("{version}", version.ToString(), StringComparison.Ordinal);
    }



    private static void ConfigureDocument(OpenApiOptions options, ApiVersion version)
    {
        options.ShouldInclude = description => description.ActionDescriptor.EndpointMetadata
            .OfType<ApiVersionMetadata>()
            .Any(metadata => metadata.IsMappedTo(version));

        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Wolfgang.Wms API";
            document.Info.Version = version.ToString();
            document.Info.Description = Frozen.Contains(version)
                ? "Frozen contract: additive changes only."
                : "Unstable contract for product 0.x: breaking changes ship in place, each with a `breaking` changelog fragment.";
            SubstituteVersionInPaths(document, version);
            document.Servers = null;   // host-neutral: the committed copy must not carry the test host's URL
            return Task.CompletedTask;
        });
    }



    private static void SubstituteVersionInPaths(OpenApiDocument document, ApiVersion version)
    {
        if (document.Paths is null || document.Paths.Count == 0)
        {
            return;
        }

        var rewritten = new OpenApiPaths();
        foreach (var (path, item) in document.Paths)
        {
            RemoveVersionParameter(item.Parameters);
            if (item.Operations is not null)
            {
                foreach (var operation in item.Operations.Values)
                {
                    RemoveVersionParameter(operation.Parameters);
                }
            }

            rewritten.Add(SubstituteVersion(path, version), item);
        }

        document.Paths = rewritten;
    }



    private static void RemoveVersionParameter(IList<IOpenApiParameter>? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        for (var i = parameters.Count - 1; i >= 0; i--)
        {
            if (parameters[i].In == ParameterLocation.Path && string.Equals(parameters[i].Name, "version", StringComparison.Ordinal))
            {
                parameters.RemoveAt(i);
            }
        }
    }
}
