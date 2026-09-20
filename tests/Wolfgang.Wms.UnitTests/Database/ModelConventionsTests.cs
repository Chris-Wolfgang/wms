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



    private static DbContextOptions<TContext> Options<TContext>(string provider)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        return (string.Equals(provider, SqlServer, StringComparison.Ordinal)
            ? builder.UseSqlServer("Server=localhost;Database=model")
            : builder.UseNpgsql("Host=localhost;Database=model")).Options;
    }
}
