// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Wolfgang.Wms.Infrastructure.Database.Sync;

/// <summary>
/// The one helper that gives every synced table its two endpoints (E5.3, E5.4):
/// <c>GET {path}?since=&amp;size=</c> → a <see cref="Delta{TItem}"/>, and <c>GET {path}/manifest</c> → the
/// <see cref="ManifestEntry"/> list. Devices call the delta often and the manifest at login, on a slow
/// schedule, on a stale-cache rejection and on a console "resync".
/// </summary>
public static class SyncEndpoints
{
    /// <summary>
    /// Maps the delta and manifest endpoints of one synced table under <paramref name="path"/>.
    /// </summary>
    /// <typeparam name="TContext">The context the table lives in.</typeparam>
    /// <typeparam name="TEntity">The synced entity.</typeparam>
    /// <typeparam name="TItem">The API record.</typeparam>
    /// <param name="endpoints">The versioned group to map into.</param>
    /// <param name="path">Relative path of the table (<c>/skus</c>).</param>
    /// <param name="source">Selects the table from the context (filters such as site scope applied here).</param>
    /// <param name="project">Projection to the API record.</param>
    /// <returns>The group holding both endpoints, for further conventions.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static RouteGroupBuilder MapSyncedTable<TContext, TEntity, TItem>(this IEndpointRouteBuilder endpoints, string path, Func<TContext, IQueryable<TEntity>> source, Func<TEntity, TItem> project)
        where TContext : DbContext
        where TEntity : class, ISyncedEntity
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(project);

        // Held in Delegate-typed locals: the route-handler analyzer crashes on lambdas whose parameters are open generic types.
        Delegate delta = async (TContext context, long? since, int? size, CancellationToken cancellationToken) =>
            TypedResults.Ok(await SyncQueries.DeltaAsync(source(context), since ?? 0, size ?? SyncQueries.DefaultPageSize, project, cancellationToken).ConfigureAwait(false));
        Delegate manifest = async (TContext context, CancellationToken cancellationToken) =>
            TypedResults.Ok(await SyncQueries.ManifestAsync(source(context), cancellationToken).ConfigureAwait(false));

        var group = endpoints.MapGroup(path);
        group.MapGet(string.Empty, delta);
        group.MapGet("/manifest", manifest);
        return group;
    }
}
