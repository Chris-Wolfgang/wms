// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Database.Conventions;
using Wolfgang.Wms.UnitTests.Database.TestModels;

namespace Wolfgang.Wms.UnitTests.Database;

/// <summary>
/// E3.1–E3.4 on a sample model built for each provider (no database needed): names, schemas, keys,
/// foreign keys, column types; and the verifier pins one message per broken convention.
/// </summary>
public sealed class ModelConventionsTests
{
    private const string SqlServer = "Microsoft.EntityFrameworkCore.SqlServer";
    private const string PostgreSql = "Npgsql.EntityFrameworkCore.PostgreSQL";



    [Theory]
    [InlineData(SqlServer)]
    [InlineData(PostgreSql)]
    public void Tables_columns_keys_and_indexes_are_snake_case_in_module_schemas(string provider)
    {
        using var context = Sample(provider);
        var container = context.Model.FindEntityType(typeof(Container))!;

        Assert.Equal("container", container.GetTableName());
        Assert.Equal("picking", container.GetSchema());
        Assert.Equal("zone_group", context.Model.FindEntityType(typeof(ZoneGroup))!.GetTableName());
        Assert.Equal(["barcode", "closed_at", "created_at", "id", "requested_qty", "type", "zone_group_id"], container.GetProperties().Select(p => p.GetColumnName()).Order(StringComparer.Ordinal));
        Assert.Equal("pk_container", container.FindPrimaryKey()!.GetName());
        Assert.Equal("fk_container_zone_group_id", container.GetForeignKeys().Single().GetConstraintName());
        Assert.Equal(["ux_container_barcode", "ix_container_zone_group_id"], container.GetIndexes().Select(i => i.GetDatabaseName()).Order(StringComparer.Ordinal).Reverse());
    }



    [Theory]
    [InlineData(SqlServer)]
    [InlineData(PostgreSql)]
    public void Primary_keys_are_server_assigned_longs_and_foreign_keys_never_cascade(string provider)
    {
        using var context = Sample(provider);
        var container = context.Model.FindEntityType(typeof(Container))!;
        var key = container.FindPrimaryKey()!.Properties.Single();

        Assert.Equal(typeof(long), key.ClrType);
        Assert.Equal(ValueGenerated.OnAdd, key.ValueGenerated);
        Assert.Equal(DeleteBehavior.Restrict, container.GetForeignKeys().Single().DeleteBehavior);
    }



    [Theory]
    [InlineData(SqlServer)]
    [InlineData(PostgreSql)]
    public void Owned_types_get_the_conventions_in_their_own_table_and_in_their_owners_table(string provider)
    {
        using var context = Sample(provider);
        var container = context.Model.FindEntityType(typeof(Container))!;
        var shipTo = container.FindNavigation(nameof(Container.ShipTo))!.TargetEntityType;
        var line = container.FindNavigation(nameof(Container.Lines))!.TargetEntityType;
        var containerTable = StoreObjectIdentifier.Create(shipTo, StoreObjectType.Table)!.Value;
        var lineTable = StoreObjectIdentifier.Create(line, StoreObjectType.Table)!.Value;

        // Table split: the owner's table is not renamed, the key stays the owner's id, other columns keep the prefix.
        Assert.Equal(("container", "picking"), (containerTable.Name, containerTable.Schema));
        Assert.Equal
        (
            ["id", "ship_to_street", "ship_to_verified_at"],
            shipTo.GetProperties().Select(p => p.GetColumnName(containerTable)).Order(StringComparer.Ordinal)
        );
        Assert.Equal("pk_container", shipTo.FindPrimaryKey()!.GetName(containerTable));

        // Own table: snake_case table, columns, key and foreign key, no database cascade.
        Assert.Equal(("container_line", "picking"), (lineTable.Name, lineTable.Schema));
        Assert.Equal
        (
            ["container_id", "id", "qty"],
            line.GetProperties().Select(p => p.GetColumnName(lineTable)).Order(StringComparer.Ordinal)
        );
        Assert.Equal("pk_container_line", line.FindPrimaryKey()!.GetName(lineTable));
        Assert.Equal("fk_container_line_container_id", line.FindOwnership()!.GetMappedConstraints().Single().Name);
        Assert.Equal(DeleteBehavior.ClientCascade, line.FindOwnership()!.DeleteBehavior);
    }



    [Theory]
    [InlineData(SqlServer)]
    [InlineData(PostgreSql)]
    public void Owned_rows_are_deleted_by_EF_with_their_owner_and_the_database_never_cascades(string provider)
    {
        using var context = Sample(provider);
        var container = new Container
        {
            Id = 1,
            Lines = { new ContainerLine { Id = 2 } },
        };
        context.Attach(container);

        context.Remove(container);

        Assert.All(context.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Deleted, entry.State));
        Assert.Equal(3, context.ChangeTracker.Entries().Count());
        Assert.DoesNotContain("CASCADE", context.Database.GenerateCreateScript(), StringComparison.OrdinalIgnoreCase);
    }



    [Theory]
    [InlineData(SqlServer, "decimal(9,3)", "datetime2(3)")]
    [InlineData(PostgreSql, "numeric(9,3)", "timestamp(3) with time zone")]
    public void Quantities_are_decimal_9_3_and_timestamps_are_utc_at_millisecond_precision(string provider, string quantityType, string timestampType)
    {
        using var context = Sample(provider);
        var container = context.Model.FindEntityType(typeof(Container))!;

        Assert.Equal(quantityType, container.FindProperty(nameof(Container.RequestedQty))!.GetColumnType());
        Assert.Equal(timestampType, container.FindProperty(nameof(Container.CreatedAt))!.GetColumnType());
        Assert.Equal(timestampType, container.FindProperty(nameof(Container.ClosedAt))!.GetColumnType());
    }



    [Theory]
    [InlineData(SqlServer, "NEXT VALUE FOR [wms].[row_version_seq]")]
    [InlineData(PostgreSql, "nextval('wms.row_version_seq')")]
    public void Versioned_entities_get_a_sequence_backed_row_version_concurrency_token_with_an_index(string provider, string defaultSql)
    {
        using var context = Sample(provider);
        var sku = context.Model.FindEntityType(typeof(Sku))!;
        var rowVersion = sku.FindProperty(nameof(Sku.RowVersion))!;

        Assert.Equal("row_version", rowVersion.GetColumnName());
        Assert.Equal(defaultSql, rowVersion.GetDefaultValueSql());
        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
        Assert.Contains(sku.GetIndexes(), i => string.Equals(i.GetDatabaseName(), "ix_sku_row_version", StringComparison.Ordinal));
        Assert.Contains(context.Model.GetSequences(), s => string.Equals(s.Name, "row_version_seq", StringComparison.Ordinal) && string.Equals(s.Schema, "wms", StringComparison.Ordinal));
        Assert.Null(context.Model.FindEntityType(typeof(Container))!.FindProperty("RowVersion"));
    }



    [Fact]
    public void SqlServer_timestamps_round_trip_as_utc_through_the_converter()
    {
        var converter = new UtcDateTimeOffsetConverter();
        var original = new DateTimeOffset(2026, 9, 20, 6, 30, 0, TimeSpan.FromHours(2));

        var stored = (DateTime)converter.ConvertToProvider(original)!;
        var loaded = (DateTimeOffset)converter.ConvertFromProvider(stored)!;

        Assert.Equal(DateTimeKind.Utc, stored.Kind);
        Assert.Equal(original.UtcDateTime, stored);
        Assert.Equal(TimeSpan.Zero, loaded.Offset);
        Assert.Equal(original, loaded);
    }



    [Theory]
    [InlineData(SqlServer)]
    [InlineData(PostgreSql)]
    public void The_sample_model_and_the_product_model_pass_verification(string provider)
    {
        using var sample = Sample(provider);
        using var product = new WmsDbContext(Options<WmsDbContext>(provider));

        Assert.Empty(ModelConventions.Verify(sample.Model));
        Assert.Empty(ModelConventions.Verify(product.Model));
    }



    [Fact]
    public void Verify_reports_every_broken_convention_by_table_and_column()
    {
        using var context = new BadModelDbContext(Options<BadModelDbContext>(PostgreSql));   // SQL Server: Apply pins timestamp precision itself

        var violations = ModelConventions.Verify(context.Model);

        Assert.Equal
        (
            [
                "BadFactoryGenerated.id: ids are assigned by the database, never by a client-side value generator.",
                "BadFactoryGenerated: table name is not snake_case.",
                "bad_child.Ref: column name is not snake_case.",
                "bad_child.amount: decimal columns are decimal(9,3).",
                "bad_child.owner: GUID columns are not allowed; identifiers are server-assigned long.",
                "bad_child.owner: every foreign key column is indexed explicitly.",
                "bad_child.owner: foreign key column must be named 'bad_parent_id'.",
                "bad_child.owner: foreign keys never cascade (delete behaviour must be Restrict).",
                "bad_child.stamp_at: use DateTimeOffset (UTC), not DateTime.",
                "bad_child.when: timestamps have precision 3.",
                "bad_child: alternate key name 'uk_bad_child_code' must start with 'ak_' and be snake_case.",
                "bad_child: foreign key name 'FK_bad_child_owner' must start with 'fk_' and be snake_case.",
                "bad_child: index name 'ux_bad_child_amount' must start with 'ix_' and be snake_case.",
                "bad_child: primary key name 'pk_BadChild' must start with 'pk_' and be snake_case.",
                "bad_child: unique index name 'ix_bad_child_ref' must start with 'ux_' and be snake_case.",
                "bad_defaulted.id: ids are identity columns (generated on add; no default or computed value).",
                "bad_defaulted.id: owned rows are deleted by EF, never by a database cascade (delete behaviour must be ClientCascade).",
                "bad_defaulted_item.id: ids are identity columns (generated on add; no default or computed value).",
                "bad_generated.id: ids are assigned by the database, never by a client-side value generator.",
                "bad_line.bad_child_id: owned rows are deleted by EF, never by a database cascade (delete behaviour must be ClientCascade).",
                "bad_line: an owned type's key columns are long (the owner's id, plus a server-assigned id in a collection).",
                "bad_line: no module schema (tables never land in dbo/public).",
                "bad_parent.created: use DateTimeOffset (UTC), not DateTime.",
                "bad_parent.id: GUID columns are not allowed; identifiers are server-assigned long.",
                "bad_parent: no module schema (tables never land in dbo/public).",
                "bad_parent: primary key must be a single server-assigned long column named 'id'.",
            ],
            violations.Order(StringComparer.Ordinal)
        );
    }



    [Theory]
    [InlineData(SqlServer)]
    [InlineData(PostgreSql)]
    public void Verify_reports_a_convention_built_name_longer_than_63_characters_on_both_providers(string provider)
    {
        using var context = new LongNamesDbContext(Options<LongNamesDbContext>(provider));

        var violations = ModelConventions.Verify(context.Model);

        Assert.Equal
        (
            [
                "inventory_adjustment_reconciliation_line: index name 'ix_inventory_adjustment_reconciliation_line_warehouse_location_identifier_reconciliation_batch_identifier' is 105 characters; identifiers are at most 63 on both providers (PostgreSQL truncates longer ones silently). Give it a shorter explicit name.",
            ],
            violations
        );
    }



    [Theory]
    [InlineData(SqlServer)]
    [InlineData(PostgreSql)]
    public void Apply_honours_an_explicit_table_name_and_ignores_the_DbSet_name(string provider)
    {
        using var context = new ExplicitNamesDbContext(Options<ExplicitNamesDbContext>(provider));

        var pickTask = context.Model.FindEntityType(typeof(WorkItem))!;
        var bin = context.Model.FindEntityType(typeof(Bin))!;

        Assert.Equal("pick_task", pickTask.GetTableName());
        Assert.Equal("picking", pickTask.GetSchema());
        Assert.Equal("pk_pick_task", pickTask.FindPrimaryKey()!.GetName());
        Assert.Equal("bin", bin.GetTableName());
        Assert.Empty(ModelConventions.Verify(context.Model));
    }



    [Fact]
    public void Members_when_the_argument_is_null_throw()
    {
        Assert.Throws<ArgumentNullException>(() => ModelConventions.Configure(null!));
        Assert.Throws<ArgumentNullException>(() => ModelConventions.Apply(null!, SqlServer));
        Assert.Throws<ArgumentNullException>(() => ModelConventions.Verify(null!));
    }



    private static SampleModelDbContext Sample(string provider)
    {
        return new SampleModelDbContext(Options<SampleModelDbContext>(provider));
    }



    private static DbContextOptions<TContext> Options<TContext>(string provider)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        return (string.Equals(provider, SqlServer, StringComparison.Ordinal)
            ? builder.UseSqlServer("Server=localhost;Database=model")
            : builder.UseNpgsql("Host=localhost;Database=model")).Options;
    }
}
