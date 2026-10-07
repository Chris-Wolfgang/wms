// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Authorization;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// The site-scope filter every collection query applies (E16.3): rows whose site is in the caller's
/// <see cref="SiteScope"/>, as one translated <c>WHERE site_id IN (...)</c>, or the query untouched when the
/// scope is unrestricted.
/// </summary>
public static class SiteScopeQueries
{
    /// <summary>The property name a site-owned row keeps its site in.</summary>
    public const string SiteIdProperty = "SiteId";



    /// <summary>
    /// Keeps the rows whose <paramref name="siteIdProperty"/> is in <paramref name="scope"/>; every row when the
    /// scope is unrestricted, none when it is empty.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IQueryable<TEntity> InScope<TEntity>(this IQueryable<TEntity> query, SiteScope scope, string siteIdProperty = SiteIdProperty)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteIdProperty);

        if (scope.IsUnrestricted)
        {
            return query;
        }

        var siteIds = scope.SiteIds;
        return query.Where(row => siteIds.Contains(EF.Property<long>(row, siteIdProperty)));
    }
}
