// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wolfgang.Wms.Infrastructure.Database.Sync;

namespace Wolfgang.Wms.Infrastructure.Database.Conventions;

/// <summary>
/// The schema conventions of E3.1–E3.4, applied to every entity once (<see cref="Apply"/>) and checked by the
/// model test (<see cref="Verify"/>): snake_case names, a module schema per table, <c>id</c> as the
/// server-assigned <c>long</c> key, <c>&lt;table&gt;_id</c> foreign keys with explicit indexes and no
/// cascades, <c>decimal(9,3)</c> quantities, UTC <c>DateTimeOffset</c> timestamps at millisecond precision,
/// and no GUID columns; plus E5.1 row versioning and E5.3 soft deletion for the entities that opt in.
/// </summary>
public static class ModelConventions
{
    /// <summary>
    /// Precision of every quantity column (max 999,999.999).
    /// </summary>
    public const int QuantityPrecision = 9;



    /// <summary>
    /// Scale of every quantity column.
    /// </summary>
    public const int QuantityScale = 3;



    /// <summary>
    /// Fractional-second precision of every timestamp (<c>datetime2(3)</c> / <c>timestamptz(3)</c>).
    /// </summary>
    public const int TimestampPrecision = 3;



    /// <summary>
    /// The engines' default schemas; no WMS table may land in them (E3.1).
    /// </summary>
    public static IReadOnlyList<string> DefaultSchemas { get; } = ["dbo", "public"];



    /// <summary>
    /// Pre-convention defaults: <c>decimal</c> → (9,3), <c>DateTimeOffset</c> → precision 3.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    public static void Configure(ModelConfigurationBuilder configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.Properties<decimal>().HavePrecision(QuantityPrecision, QuantityScale);
        configuration.Properties<DateTimeOffset>().HavePrecision(TimestampPrecision);
    }



    /// <summary>
    /// Names every table, column, key, foreign key and index in snake_case, makes every foreign key
    /// <see cref="DeleteBehavior.Restrict"/>, and on SQL Server stores timestamps as UTC <c>datetime2(3)</c>.
    /// Schemas are not assigned here: each module configures its own (<c>ToTable(name, schema)</c>), and
    /// <see cref="Verify"/> rejects an entity without one.
    /// </summary>
    /// <param name="modelBuilder">The model being built.</param>
    /// <param name="providerName">The EF provider name (<c>Database.ProviderName</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="modelBuilder"/> is null.</exception>
    public static void Apply(ModelBuilder modelBuilder, string? providerName)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var sqlServer = string.Equals(providerName, RowVersioning.SqlServer, StringComparison.Ordinal);
        modelBuilder.HasSequence<long>(RowVersioning.SequenceName, RowVersioning.Schema).StartsAt(1).IncrementsBy(1);
        foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(e => !e.IsOwned()))
        {
            var table = SnakeCase.Of(entity.ClrType.Name);
            entity.SetTableName(table);
            if (typeof(IVersionedEntity).IsAssignableFrom(entity.ClrType))
            {
                ConfigureRowVersion(entity, providerName, table);
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(entity.ClrType))
            {
                ConfigureSoftDelete(entity);
            }

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(SnakeCase.Of(property.Name));
                if (sqlServer && (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?)))
                {
                    property.SetValueConverter(new UtcDateTimeOffsetConverter());
                    property.SetPrecision(TimestampPrecision);
                }
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(key.IsPrimaryKey() ? "pk_" + table : "ak_" + table + "_" + Columns(key.Properties));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName("fk_" + table + "_" + Columns(foreignKey.Properties));
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName((index.IsUnique ? "ux_" : "ix_") + table + "_" + Columns(index.Properties));
            }
        }
    }



    /// <summary>
    /// Every convention a module's configuration can still break, as one message per violation; empty when
    /// the model complies. The model test asserts it is empty.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public static IReadOnlyList<string> Verify(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var violations = new List<string>();
        foreach (var entity in model.GetEntityTypes().Where(e => !e.IsOwned()))
        {
            var table = entity.GetTableName() ?? entity.ClrType.Name;
            var schema = entity.GetSchema();
            if (string.IsNullOrEmpty(schema) || DefaultSchemas.Contains(schema, StringComparer.OrdinalIgnoreCase))
            {
                violations.Add($"{table}: no module schema (tables never land in {string.Join('/', DefaultSchemas)}).");
            }

            if (!SnakeCase.Is(table))
            {
                violations.Add($"{table}: table name is not snake_case.");
            }

            foreach (var property in entity.GetProperties())
            {
                if (!SnakeCase.Is(property.GetColumnName()))
                {
                    violations.Add($"{table}.{property.GetColumnName()}: column name is not snake_case.");
                }
            }

            if (IsLibraryOwned(entity.ClrType))
            {
                continue;   // a library's tables keep the library's shape; only schema and naming are ours
            }

            VerifyKey(entity, table, violations);
            VerifyRowVersion(entity, table, violations);
            VerifySoftDelete(entity, table, violations);
            foreach (var property in entity.GetProperties())
            {
                VerifyProperty(property, table, violations);
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                VerifyForeignKey(entity, foreignKey, table, violations);
            }
        }

        return violations;
    }



    /// <summary>
    /// E5.1: <c>row_version</c> is a database-assigned bigint (sequence default on insert, trigger on update),
    /// the concurrency token, and indexed because it is the sync watermark. The update trigger is declared
    /// on the entity so EF reads generated values back with a query instead of an <c>OUTPUT</c> clause, which
    /// SQL Server refuses on a table with triggers.
    /// </summary>
    private static void ConfigureRowVersion(IMutableEntityType entity, string? providerName, string table)
    {
        if (entity.FindDeclaredTrigger(RowVersioning.TriggerName(table)) is null)
        {
            entity.AddTrigger(RowVersioning.TriggerName(table));
        }

        var property = entity.FindProperty(nameof(IVersionedEntity.RowVersion)) ?? entity.AddProperty(nameof(IVersionedEntity.RowVersion), typeof(long));
        property.ValueGenerated = ValueGenerated.OnAddOrUpdate;
        property.IsConcurrencyToken = true;
        property.SetDefaultValueSql(RowVersioning.DefaultValueSql(providerName));
        if (entity.FindIndex(property) is null)
        {
            entity.AddIndex(property);
        }
    }



    /// <summary>
    /// E5.3: soft-deleted rows are hidden from every query by default; delta reads opt out with
    /// <c>IgnoreQueryFilters()</c>.
    /// </summary>
    private static void ConfigureSoftDelete(IMutableEntityType entity)
    {
        if (entity.FindProperty(nameof(ISoftDeletable.DeletedAt)) is null)
        {
            return;
        }

        var row = Expression.Parameter(entity.ClrType, "e");
        var live = Expression.Equal(Expression.Property(row, nameof(ISoftDeletable.DeletedAt)), Expression.Constant(null, typeof(DateTimeOffset?)));
        entity.SetQueryFilter(Expression.Lambda(live, row));
    }



    private static void VerifySoftDelete(IEntityType entity, string table, List<string> violations)
    {
        if (!typeof(ISoftDeletable).IsAssignableFrom(entity.ClrType))
        {
            return;
        }

        var property = entity.FindProperty(nameof(ISoftDeletable.DeletedAt));
        if (property is null || property.ClrType != typeof(DateTimeOffset?) || !string.Equals(property.GetColumnName(), "deleted_at", StringComparison.Ordinal))
        {
            violations.Add($"{table}: soft-deletable entities carry a nullable deleted_at timestamp.");
        }

        if (entity.GetDeclaredQueryFilters().Count == 0)
        {
            violations.Add($"{table}: soft-deletable tables hide deleted rows by default (query filter).");
        }
    }



    private static void VerifyRowVersion(IEntityType entity, string table, List<string> violations)
    {
        if (!typeof(IVersionedEntity).IsAssignableFrom(entity.ClrType))
        {
            return;
        }

        var property = entity.FindProperty(nameof(IVersionedEntity.RowVersion));
        if (property is null || property.ClrType != typeof(long) || !property.IsConcurrencyToken
            || property.ValueGenerated != ValueGenerated.OnAddOrUpdate
            || !string.Equals(property.GetColumnName(), RowVersioning.ColumnName, StringComparison.Ordinal)
            || string.IsNullOrEmpty(property.GetDefaultValueSql()))
        {
            violations.Add($"{table}: versioned entities carry a database-assigned long row_version concurrency token.");
        }

        if (property is not null && entity.FindIndex(property) is null)
        {
            violations.Add($"{table}.row_version: versioned tables index row_version (sync watermark).");
        }
    }



    private static void VerifyKey(IEntityType entity, string table, List<string> violations)
    {
        var key = entity.FindPrimaryKey();
        if (key is null || key.Properties.Count != 1 || key.Properties[0].ClrType != typeof(long)
            || !string.Equals(key.Properties[0].GetColumnName(), "id", StringComparison.Ordinal)
            || key.Properties[0].ValueGenerated != ValueGenerated.OnAdd)
        {
            violations.Add($"{table}: primary key must be a single server-assigned long column named 'id'.");
        }
    }



    /// <summary>
    /// True for an entity a library maps into the model (E6.4: AuditTrail's tables), which keeps the
    /// library's keys and column types; the conventions still name it and place it in a module schema.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="clrType"/> is null.</exception>
    public static bool IsLibraryOwned(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        return !(clrType.Assembly.GetName().Name ?? string.Empty).StartsWith("Wolfgang.Wms", StringComparison.Ordinal);
    }



    private static void VerifyProperty(IProperty property, string table, List<string> violations)
    {
        var column = property.GetColumnName();
        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (type == typeof(Guid))
        {
            violations.Add($"{table}.{column}: GUID columns are not allowed; identifiers are server-assigned long.");
        }

        if (type == typeof(DateTime))
        {
            violations.Add($"{table}.{column}: use DateTimeOffset (UTC), not DateTime.");
        }

        if (type == typeof(decimal) && (property.GetPrecision() != QuantityPrecision || property.GetScale() != QuantityScale))
        {
            violations.Add($"{table}.{column}: decimal columns are decimal({QuantityPrecision},{QuantityScale}).");
        }

        if (type == typeof(DateTimeOffset) && property.GetPrecision() != TimestampPrecision)
        {
            violations.Add($"{table}.{column}: timestamps have precision {TimestampPrecision}.");
        }
    }



    private static void VerifyForeignKey(IEntityType entity, IForeignKey foreignKey, string table, List<string> violations)
    {
        var columns = Columns(foreignKey.Properties);
        if (foreignKey.Properties.Count == 1)
        {
            var expected = (foreignKey.PrincipalEntityType.GetTableName() ?? foreignKey.PrincipalEntityType.ClrType.Name) + "_id";
            if (!string.Equals(columns, expected, StringComparison.Ordinal))
            {
                violations.Add($"{table}.{columns}: foreign key column must be named '{expected}'.");
            }
        }

        if (foreignKey.DeleteBehavior != DeleteBehavior.Restrict)
        {
            violations.Add($"{table}.{columns}: foreign keys never cascade (delete behaviour must be Restrict).");
        }

        var indexed = entity.GetIndexes().Any(i => i.Properties.Select(p => p.Name).SequenceEqual(foreignKey.Properties.Select(p => p.Name), StringComparer.Ordinal))
            || (entity.FindPrimaryKey()?.Properties.Select(p => p.Name).SequenceEqual(foreignKey.Properties.Select(p => p.Name), StringComparer.Ordinal) ?? false);
        if (!indexed)
        {
            violations.Add($"{table}.{columns}: every foreign key column is indexed explicitly.");
        }
    }



    private static string Columns(IReadOnlyList<IReadOnlyProperty> properties)
    {
        return string.Join('_', properties.Select(p => p.GetColumnName() ?? SnakeCase.Of(p.Name)));
    }
}
