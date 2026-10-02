// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Caching;
using Wolfgang.Wms.Infrastructure.Database.Conventions;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// <see cref="IRowVersionSource"/> over the product model (E1.12, E6.3): one <c>MAX(row_version)</c> plus
/// <c>COUNT(*)</c> per entity type, the table identifiers taken from the model (the caller names a type,
/// never a table) so only versioned tables of the schema can be asked about. Runs in its own scope because
/// the caches that use it are singletons.
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
    /// <exception cref="ArgumentException">An entity type is not a versioned entity of the model.</exception>
    public async Task<RowVersionStamp> GetStampAsync(IReadOnlyCollection<Type> entityTypes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entityTypes);

        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var max = 0UL;
        var count = 0L;
        foreach (var entityType in entityTypes)
        {
            var sql = Sql(context, entityType);
            var row = await context.Database.SqlQueryRaw<StampRow>(sql).SingleAsync(cancellationToken).ConfigureAwait(false);
            max = Math.Max(max, (ulong)Math.Max(0, row.MaxVersion));
            count += row.RowCount;
        }

        return new RowVersionStamp(max, count);
    }



    /// <summary>
    /// The query for one entity type's table, built from the model's own identifiers: the highest row
    /// version and the row count in one round trip.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="entityType"/> is not a versioned entity of the model.</exception>
    public static string Sql(WmsDbContext context, Type entityType)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entityType);

        var entity = context.Model.FindEntityType(entityType);
        if (entity is null || !typeof(IVersionedEntity).IsAssignableFrom(entity.ClrType))
        {
            throw new ArgumentException($"'{entityType.Name}' is not a versioned entity of the model.", nameof(entityType));
        }

        var schema = entity.GetSchema()!;
        var name = entity.GetTableName()!;
        return string.Equals(context.Database.ProviderName, RowVersioning.SqlServer, StringComparison.Ordinal)
            ? "SELECT COALESCE(MAX([" + RowVersioning.ColumnName + "]), 0) AS [MaxVersion], COUNT_BIG(*) AS [RowCount] FROM [" + schema + "].[" + name + "]"
            : "SELECT COALESCE(MAX(" + RowVersioning.ColumnName + "), 0) AS \"MaxVersion\", COUNT(*) AS \"RowCount\" FROM " + schema + "." + name;
    }



    /// <summary>
    /// One row of <see cref="Sql"/>: the projection EF materialises from the raw query.
    /// </summary>
    private sealed class StampRow
    {
        public long MaxVersion { get; init; }

        public long RowCount { get; init; }
    }
}
