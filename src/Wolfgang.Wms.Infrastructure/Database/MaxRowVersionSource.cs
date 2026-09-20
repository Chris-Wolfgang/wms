// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Caching;
using Wolfgang.Wms.Infrastructure.Database.Conventions;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// <see cref="IRowVersionSource"/> over the product model (E1.12, E6.3): one <c>MAX(row_version)</c> per
/// table, the identifiers taken from the model (never from the caller) so only versioned tables of the
/// schema can be asked about. Runs in its own scope because the caches that use it are singletons.
/// </summary>
public sealed class MaxRowVersionSource : IRowVersionSource
{
    private readonly IServiceScopeFactory _scopes;



    /// <summary>
    /// Creates the source.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="scopes"/> is null.</exception>
    public MaxRowVersionSource(IServiceScopeFactory scopes)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentException">A table is not a versioned table of the model.</exception>
    public async Task<ulong> GetMaxRowVersionAsync(IReadOnlyCollection<string> tables, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tables);

        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var max = 0UL;
        foreach (var table in tables)
        {
            var sql = Sql(context, table);
            var version = await context.Database.SqlQueryRaw<long>(sql).SingleAsync(cancellationToken).ConfigureAwait(false);
            max = Math.Max(max, (ulong)Math.Max(0, version));
        }

        return max;
    }



    /// <summary>
    /// The query for one table, built from the model's own identifiers.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="table"/> is not a versioned table of the model.</exception>
    public static string Sql(WmsDbContext context, string table)
    {
        ArgumentNullException.ThrowIfNull(context);

        var entity = context.Model.GetEntityTypes()
            .FirstOrDefault(e => typeof(IVersionedEntity).IsAssignableFrom(e.ClrType) && string.Equals(e.GetSchema() + "." + e.GetTableName(), table, StringComparison.Ordinal))
            ?? throw new ArgumentException($"'{table}' is not a versioned table of the model.", nameof(table));
        var schema = entity.GetSchema()!;
        var name = entity.GetTableName()!;
        return string.Equals(context.Database.ProviderName, RowVersioning.SqlServer, StringComparison.Ordinal)
            ? "SELECT COALESCE(MAX([" + RowVersioning.ColumnName + "]), 0) AS [Value] FROM [" + schema + "].[" + name + "]"
            : "SELECT COALESCE(MAX(" + RowVersioning.ColumnName + "), 0) AS \"Value\" FROM " + schema + "." + name;
    }
}
