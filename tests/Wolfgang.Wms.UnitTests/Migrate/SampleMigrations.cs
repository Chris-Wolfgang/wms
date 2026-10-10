// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.UnitTests.Migrate;

/// <summary>
/// A migrations assembly of its own for <see cref="MigrationRunner"/> tests that need a shipped migration whose
/// <c>Down</c> loses data (the product's only migration, <c>Initial</c>, has an empty <c>Down</c>). A context
/// built by <see cref="Context"/> discovers these instead of the product's; nothing else sees them.
/// </summary>
internal static class SampleMigrations
{
    public const string Base = "20990101000000_SampleBase";

    public const string RawSql = "20990102000000_SampleRawSql";

    public const string Drops = "20990102000001_SampleDrops";



    public static WmsDbContext Context()
    {
        var builder = new DbContextOptionsBuilder<WmsDbContext>();
        builder.UseNpgsql
        (
            "Host=nowhere",
            npgsql => npgsql
                .MigrationsAssembly(typeof(SampleMigrations).Assembly.GetName().Name)
                .MigrationsHistoryTable(DatabaseServiceCollectionExtensions.HistoryTable, DatabaseServiceCollectionExtensions.HistorySchema)
        );
        return new WmsDbContext(builder.Options);
    }



    [DbContext(typeof(WmsDbContext))]
    [Migration(Base)]
    internal sealed class SampleBase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema("sample");
        }



        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }



    [DbContext(typeof(WmsDbContext))]
    [Migration(RawSql)]
    internal sealed class SampleRawSql : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE TABLE sample.note (id integer)");
        }



        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM sample.note;\nDROP TABLE sample.note");
        }
    }



    [DbContext(typeof(WmsDbContext))]
    [Migration(Drops)]
    internal sealed class SampleDrops : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "size", table: "note", schema: "sample", nullable: true);
        }



        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("size", "note", "sample");
        }
    }
}
