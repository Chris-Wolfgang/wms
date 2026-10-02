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



    [Theory]
    [InlineData(SqlServer)]
    [InlineData(PostgreSql)]
    public void Soft_deletable_entities_hide_deleted_rows_by_default_and_versioned_ones_declare_their_trigger(string provider)
    {
        using var context = Sample(provider);
        var sku = context.Model.FindEntityType(typeof(Sku))!;

        Assert.Equal("deleted_at", sku.FindProperty(nameof(Sku.DeletedAt))!.GetColumnName());
        Assert.Single(sku.GetDeclaredQueryFilters());
        Assert.Equal(["trg_sku_row_version"], sku.GetDeclaredTriggers().Select(t => t.ModelName));
        Assert.Empty(context.Model.FindEntityType(typeof(Container))!.GetDeclaredTriggers());
    }



    [Fact]
    public void Timestamps_are_truncated_to_the_millisecond_on_both_providers()
    {
        var boundary = new DateTimeOffset(2026, 9, 20, 6, 30, 0, 56, TimeSpan.Zero).AddTicks(9995);   // the servers would round this up to .057

        Assert.Equal(new DateTimeOffset(2026, 9, 20, 6, 30, 0, 56, TimeSpan.Zero), Timestamps.TruncateToMillisecond(boundary));
        Assert.Equal(new DateTime(2026, 9, 20, 6, 30, 0, 56, DateTimeKind.Utc), (DateTime)new UtcDateTimeOffsetConverter().ConvertToProvider(boundary)!);
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 6, 30, 0, 56, TimeSpan.Zero), (DateTimeOffset)new MillisecondDateTimeOffsetConverter().ConvertToProvider(boundary)!);
        Assert.Equal(boundary, (DateTimeOffset)new MillisecondDateTimeOffsetConverter().ConvertFromProvider(boundary)!);
        Assert.Equal(TimeSpan.Zero, Timestamps.TruncateToMillisecond(new DateTimeOffset(2026, 9, 20, 8, 30, 0, TimeSpan.FromHours(2))).Offset);
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
                "bad_child.amount: decimal columns are decimal(9,3).",
                "bad_child.owner: GUID columns are not allowed; identifiers are server-assigned long.",
                "bad_child.owner: every foreign key column is indexed explicitly.",
                "bad_child.owner: foreign key column must be named 'bad_parent_id'.",
                "bad_child.owner: foreign keys never cascade (delete behaviour must be Restrict).",
                "bad_child.when: timestamps have precision 3.",
                "bad_parent.created: use DateTimeOffset (UTC), not DateTime.",
                "bad_parent.id: GUID columns are not allowed; identifiers are server-assigned long.",
                "bad_parent: no module schema (tables never land in dbo/public).",
                "bad_parent: primary key must be a single server-assigned long column named 'id'.",
                "bad_soft: soft-deletable entities carry a nullable deleted_at timestamp.",
                "bad_soft: soft-deletable tables hide deleted rows by default (query filter).",
            ],
            violations.Order(StringComparer.Ordinal)
        );
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



    internal static DbContextOptions<TContext> Options<TContext>(string provider)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        return (string.Equals(provider, SqlServer, StringComparison.Ordinal)
            ? builder.UseSqlServer("Server=localhost;Database=model")
            : builder.UseNpgsql("Host=localhost;Database=model")).Options;
    }
}
