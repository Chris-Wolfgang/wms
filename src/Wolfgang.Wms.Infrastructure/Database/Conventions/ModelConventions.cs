// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Wolfgang.Wms.Infrastructure.Database.Conventions;

/// <summary>
/// The schema conventions of E3.1–E3.4, applied to every entity once (<see cref="Apply"/>) and checked by the
/// model test (<see cref="Verify"/>): snake_case names, a module schema per table, <c>id</c> as the
/// server-assigned <c>long</c> key, <c>&lt;table&gt;_id</c> foreign keys with explicit indexes and no
/// database cascades, <c>decimal(9,3)</c> quantities, UTC <c>DateTimeOffset</c> timestamps at millisecond
/// precision, and no GUID columns. Owned entity types follow the same rules, whether they have a table of their
/// own or share their owner's.
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
    /// <see cref="DeleteBehavior.Restrict"/> (an ownership <see cref="DeleteBehavior.ClientCascade"/>: EF deletes
    /// the owned rows with their owner, the database never cascades), and on SQL Server stores timestamps as UTC
    /// <c>datetime2(3)</c>. An owned type with a table of its own gets a snake_case table name; one that shares
    /// its owner's table leaves that table and the owner's key column alone, and its other columns keep EF's
    /// navigation prefix (<c>ShipTo.Street</c> → <c>ship_to_street</c>). Schemas are not assigned here: each
    /// module configures its own (<c>ToTable(name, schema)</c>), and <see cref="Verify"/> rejects a table
    /// without one.
    /// </summary>
    /// <param name="modelBuilder">The model being built.</param>
    /// <param name="providerName">The EF provider name (<c>Database.ProviderName</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="modelBuilder"/> is null.</exception>
    public static void Apply(ModelBuilder modelBuilder, string? providerName)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var sqlServer = string.Equals(providerName, RowVersioning.SqlServer, StringComparison.Ordinal);
        modelBuilder.HasSequence<long>(RowVersioning.SequenceName, RowVersioning.Schema).StartsAt(1).IncrementsBy(1);

        // Owners first, so an owned type compares itself with its owner's final table name.
        foreach (var entity in modelBuilder.Model.GetEntityTypes().OrderBy(OwnershipDepth).ToList())
        {
            var ownership = entity.FindOwnership();
            var sharesOwnerTable = ownership is not null && SharesTable(entity, ownership.PrincipalEntityType);
            if (ownership is null)
            {
                entity.SetTableName(SnakeCase.Of(entity.ClrType.Name));
            }
            else if (!sharesOwnerTable)
            {
                entity.SetTableName(SnakeCase.Of(entity.GetTableName()!));
            }

            // E5.1: versioned (non-owned) entities get the row_version column and index before names are applied.
            if (ownership is null && typeof(IVersionedEntity).IsAssignableFrom(entity.ClrType))
            {
                ConfigureRowVersion(entity, providerName);
            }

            var storeObject = StoreObjectIdentifier.Create(entity, StoreObjectType.Table)!.Value;
            foreach (var property in entity.GetProperties())
            {
                ApplyProperty(property, storeObject, sharesOwnerTable, sqlServer);
            }

            ApplyNames(entity, storeObject);
        }
    }



    /// <summary>
    /// Every convention a module's configuration can still break, as one message per violation; empty when
    /// the model complies. The model test asserts it is empty. Names are checked as the database sees them
    /// (the table each entity type, owned or not, is mapped to).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public static IReadOnlyList<string> Verify(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var violations = new List<string>();
        foreach (var entity in model.GetEntityTypes())
        {
            var storeObject = StoreObjectIdentifier.Create(entity, StoreObjectType.Table) ?? StoreObjectIdentifier.Table(entity.ShortName());
            var table = storeObject.Name;
            if (string.IsNullOrEmpty(storeObject.Schema) || DefaultSchemas.Contains(storeObject.Schema, StringComparer.OrdinalIgnoreCase))
            {
                violations.Add($"{table}: no module schema (tables never land in {string.Join('/', DefaultSchemas)}).");
            }

            if (!SnakeCase.Is(table))
            {
                violations.Add($"{table}: table name is not snake_case.");
            }

            VerifyKeys(entity, storeObject, violations);
            VerifyRowVersion(entity, table, violations);
            foreach (var property in entity.GetProperties())
            {
                VerifyProperty(property, storeObject, violations);
            }

            // A foreign key between types sharing one table (an owned type's link to its owner) has no constraint, so
            // only its delete behaviour is checked.
            foreach (var foreignKey in entity.GetForeignKeys())
            {
                VerifyForeignKey(entity, foreignKey, storeObject, violations);
            }

            foreach (var index in entity.GetIndexes())
            {
                VerifyName
                (
                    table,
                    index.IsUnique ? "unique index" : "index",
                    index.GetDatabaseName(storeObject),
                    index.IsUnique ? "ux_" : "ix_",
                    violations
                );
            }
        }

        return violations.Distinct(StringComparer.Ordinal).ToList();
    }



    /// <summary>
    /// E5.1: <c>row_version</c> is a database-assigned bigint (sequence default on insert, trigger on update),
    /// the concurrency token, and indexed because it is the sync watermark.
    /// </summary>
    private static void ConfigureRowVersion(IMutableEntityType entity, string? providerName)
    {
        var property = entity.FindProperty(nameof(IVersionedEntity.RowVersion)) ?? entity.AddProperty(nameof(IVersionedEntity.RowVersion), typeof(long));
        property.ValueGenerated = ValueGenerated.OnAddOrUpdate;
        property.IsConcurrencyToken = true;
        property.SetDefaultValueSql(RowVersioning.DefaultValueSql(providerName));
        if (entity.FindIndex(property) is null)
        {
            entity.AddIndex(property);
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



    private static int OwnershipDepth(IReadOnlyEntityType entity)
    {
        var ownership = entity.FindOwnership();
        return ownership is null ? 0 : 1 + OwnershipDepth(ownership.PrincipalEntityType);
    }



    private static bool SharesTable(IReadOnlyEntityType entity, IReadOnlyEntityType other)
    {
        return string.Equals(entity.GetTableName(), other.GetTableName(), StringComparison.Ordinal)
            && string.Equals(entity.GetSchema(), other.GetSchema(), StringComparison.Ordinal);
    }



    private static void ApplyProperty(IMutableProperty property, StoreObjectIdentifier storeObject, bool sharesOwnerTable, bool sqlServer)
    {
        if (!sharesOwnerTable)
        {
            property.SetColumnName(SnakeCase.Of(property.Name));
        }
        else if (!property.IsPrimaryKey())
        {
            // Shared table: EF's navigation prefix keeps two owned values of one type apart; the key column is
            // left alone so it stays mapped to the owner's id column.
            property.SetColumnName(SnakeCase.Of(property.GetDefaultColumnName(storeObject)!));
        }

        if (sqlServer && (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?)))
        {
            property.SetValueConverter(new UtcDateTimeOffsetConverter());
            property.SetPrecision(TimestampPrecision);
        }
    }



    private static void ApplyNames(IMutableEntityType entity, StoreObjectIdentifier storeObject)
    {
        var table = storeObject.Name;
        foreach (var key in entity.GetKeys())
        {
            key.SetName(key.IsPrimaryKey() ? "pk_" + table : "ak_" + table + "_" + Columns(key.Properties, storeObject));
        }

        foreach (var foreignKey in entity.GetForeignKeys())
        {
            foreignKey.SetConstraintName("fk_" + table + "_" + Columns(foreignKey.Properties, storeObject));
            foreignKey.DeleteBehavior = foreignKey.IsOwnership ? DeleteBehavior.ClientCascade : DeleteBehavior.Restrict;
        }

        foreach (var index in entity.GetIndexes())
        {
            index.SetDatabaseName((index.IsUnique ? "ux_" : "ix_") + table + "_" + Columns(index.Properties, storeObject));
        }
    }



    private static void VerifyKeys(IEntityType entity, StoreObjectIdentifier storeObject, List<string> violations)
    {
        var table = storeObject.Name;
        var key = entity.FindPrimaryKey();
        if (entity.IsOwned())
        {
            // An owned type's key is its owner's id (plus its own server-assigned id in a collection).
            if (key is null || key.Properties.Any(p => p.ClrType != typeof(long)))
            {
                violations.Add($"{table}: an owned type's key columns are long (the owner's id, plus a server-assigned id in a collection).");
            }
        }
        else if (key is null || key.Properties.Count != 1 || key.Properties[0].ClrType != typeof(long)
            || !string.Equals(key.Properties[0].GetColumnName(storeObject), "id", StringComparison.Ordinal)
            || key.Properties[0].ValueGenerated != ValueGenerated.OnAdd)
        {
            violations.Add($"{table}: primary key must be a single server-assigned long column named 'id'.");
        }

        // ValueGenerated.OnAdd alone does not prove the database assigns the value: a configured generator runs in
        // the client (HasValueGenerator / HasValueGeneratorFactory).
        foreach (var property in entity.GetProperties().Where(p => p.IsPrimaryKey() && p.GetValueGeneratorFactory() is not null))
        {
            violations.Add($"{table}.{property.GetColumnName(storeObject)}: ids are assigned by the database, never by a client-side value generator.");
        }

        // The table's own id (every key column except an owned type's link to its owner) is an identity column: generated
        // on add, and never a column default or computed value, which would not keep ids unique.
        var ownerLink = entity.FindOwnership()?.Properties ?? [];
        foreach (var property in key?.Properties.Where(p => !ownerLink.Contains(p)) ?? [])
        {
            if (property.ValueGenerated != ValueGenerated.OnAdd || property.GetDefaultValueSql(storeObject) is not null
                || property.TryGetDefaultValue(storeObject, out _) || property.GetComputedColumnSql(storeObject) is not null)
            {
                violations.Add($"{table}.{property.GetColumnName(storeObject)}: ids are identity columns (generated on add; no default or computed value).");
            }
        }

        foreach (var candidate in entity.GetKeys())
        {
            VerifyName
            (
                table,
                candidate.IsPrimaryKey() ? "primary key" : "alternate key",
                candidate.GetName(storeObject),
                candidate.IsPrimaryKey() ? "pk_" : "ak_",
                violations
            );
        }
    }



    private static void VerifyProperty(IProperty property, StoreObjectIdentifier storeObject, List<string> violations)
    {
        var table = storeObject.Name;
        var column = property.GetColumnName(storeObject);
        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (!SnakeCase.Is(column))
        {
            violations.Add($"{table}.{column}: column name is not snake_case.");
        }

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



    private static void VerifyForeignKey(IEntityType entity, IForeignKey foreignKey, StoreObjectIdentifier storeObject, List<string> violations)
    {
        var table = storeObject.Name;
        var columns = Columns(foreignKey.Properties, storeObject);
        if (foreignKey.IsOwnership && foreignKey.DeleteBehavior != DeleteBehavior.ClientCascade)
        {
            violations.Add($"{table}.{columns}: owned rows are deleted by EF, never by a database cascade (delete behaviour must be ClientCascade).");
        }

        var constraint = foreignKey.GetMappedConstraints().FirstOrDefault();
        if (constraint is null)
        {
            return;
        }

        if (foreignKey.Properties.Count == 1)
        {
            var expected = foreignKey.PrincipalEntityType.GetTableName() + "_id";
            if (!string.Equals(columns, expected, StringComparison.Ordinal))
            {
                violations.Add($"{table}.{columns}: foreign key column must be named '{expected}'.");
            }
        }

        if (!foreignKey.IsOwnership && foreignKey.DeleteBehavior != DeleteBehavior.Restrict)
        {
            violations.Add($"{table}.{columns}: foreign keys never cascade (delete behaviour must be Restrict).");
        }

        // Any key or index that leads with the foreign key's columns serves it (e.g. an owned collection's
        // (owner_id, id) primary key).
        var foreignKeyProperties = foreignKey.Properties.Select(p => p.Name).ToList();
        var indexed = entity.GetIndexes()
            .Select(i => i.Properties)
            .Concat(entity.GetKeys().Select(k => k.Properties))
            .Any(properties => properties.Select(p => p.Name).Take(foreignKeyProperties.Count).SequenceEqual(foreignKeyProperties, StringComparer.Ordinal));
        if (!indexed)
        {
            violations.Add($"{table}.{columns}: every foreign key column is indexed explicitly.");
        }

        VerifyName(table, "foreign key", constraint.Name, "fk_", violations);
    }



    private static void VerifyName(string table, string kind, string? name, string prefix, List<string> violations)
    {
        if (name is null || !name.StartsWith(prefix, StringComparison.Ordinal) || !SnakeCase.Is(name))
        {
            violations.Add($"{table}: {kind} name '{name}' must start with '{prefix}' and be snake_case.");
        }
    }



    private static string Columns(IReadOnlyList<IReadOnlyProperty> properties, StoreObjectIdentifier storeObject)
    {
        return string.Join('_', properties.Select(p => p.GetColumnName(storeObject)));
    }
}
