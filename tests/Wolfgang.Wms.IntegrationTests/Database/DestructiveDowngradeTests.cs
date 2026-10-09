// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Migrate;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E4.1 / E4.6 against each engine with migrations of this assembly's own (the product's <c>Initial</c> loses
/// nothing on the way down and cannot fail): an applied downgrade whose <c>Down</c> drops a schema or runs raw
/// SQL is refused without <c>--confirm-data-loss</c> and runs with it, and a failing migration is named with
/// the ones before it kept.
/// </summary>
public sealed class DestructiveDowngradeTests
{
    private const string Base = "20990101000000_ItBase";

    private const string RawSql = "20990102000000_ItRawSql";

    private const string Broken = "20990103000000_ItBroken";



    [SqlServerFact]
    public async Task SqlServer_a_data_losing_downgrade_needs_confirmation_and_a_failing_migration_is_named()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();

        await AssertDowngradeAsync("SqlServer", database.ConnectionString);
    }



    [DockerFact]
    public async Task PostgreSql_a_data_losing_downgrade_needs_confirmation_and_a_failing_migration_is_named()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertDowngradeAsync("PostgreSql", container.GetConnectionString());
    }



    private static async Task AssertDowngradeAsync(string provider, string connectionString)
    {
        using var context = Context(provider, connectionString);
        var runner = new MigrationRunner(context);

        var up = await ApplyAsync(runner, ["--to", RawSql]);
        var broken = await ApplyAsync(runner, []);
        var refused = await ApplyAsync(runner, ["--to", "0"]);
        var stillThere = await context.Database.SqlQueryRaw<int>("SELECT count(*) AS \"Value\" FROM sample.note").SingleAsync();
        var confirmed = await ApplyAsync(runner, ["--to", "0", "--confirm-data-loss"]);
        var status = await runner.StatusAsync(CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitOk, up.Code);
        Assert.Equal(MigrateProgram.ExitMigrationFailed, broken.Code);
        Assert.Contains("Migration " + Broken + " failed:", broken.Error, StringComparison.Ordinal);
        Assert.Contains("Applied (0)", broken.Output, StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitConfirmationRequired, refused.Code);
        Assert.Contains("This downgrade loses data; re-run with --confirm-data-loss to proceed:", refused.Error, StringComparison.Ordinal);
        Assert.Contains(RawSql + ": raw SQL, not inspected: DELETE FROM sample.note", refused.Error, StringComparison.Ordinal);
        Assert.Contains(Base + ": drop schema sample", refused.Error, StringComparison.Ordinal);
        Assert.Equal(1, stillThere);
        Assert.Equal(MigrateProgram.ExitOk, confirmed.Code);
        Assert.Contains("Reverted (2)", confirmed.Output, StringComparison.Ordinal);
        Assert.Empty(status.Applied);
        Assert.Equal(["raw SQL, not inspected: SELECT 1"], MigrationRunner.DestructiveOperationsIn(new ItBroken()));
    }



    private static WmsDbContext Context(string provider, string connectionString)
    {
        var builder = new DbContextOptionsBuilder<WmsDbContext>();
        if (string.Equals(provider, "SqlServer", StringComparison.Ordinal))
        {
            builder.UseSqlServer
            (
                connectionString,
                sqlServer => sqlServer
                    .MigrationsAssembly(typeof(DestructiveDowngradeTests).Assembly.GetName().Name)
                    .MigrationsHistoryTable(DatabaseServiceCollectionExtensions.HistoryTable, DatabaseServiceCollectionExtensions.HistorySchema)
            );
        }
        else
        {
            builder.UseNpgsql
            (
                connectionString,
                npgsql => npgsql
                    .MigrationsAssembly(typeof(DestructiveDowngradeTests).Assembly.GetName().Name)
                    .MigrationsHistoryTable(DatabaseServiceCollectionExtensions.HistoryTable, DatabaseServiceCollectionExtensions.HistorySchema)
            );
        }

        return new WmsDbContext(builder.Options);
    }



    private static async Task<(int Code, string Output, string Error)> ApplyAsync(MigrationRunner runner, string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await MigrateProgram.ApplyAsync(runner, MigrateCommandLine.Parse(args), output, error, CancellationToken.None);
        return (code, output.ToString(), error.ToString());
    }



    [DbContext(typeof(WmsDbContext))]
    [Migration(Base)]
    internal sealed class ItBase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema("sample");
            migrationBuilder.Sql("CREATE TABLE sample.note (id integer)");
        }



        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("note", "sample");
            migrationBuilder.DropSchema("sample");
        }
    }



    [DbContext(typeof(WmsDbContext))]
    [Migration(RawSql)]
    internal sealed class ItRawSql : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("INSERT INTO sample.note (id) VALUES (1)");
        }



        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM sample.note");
        }
    }



    [DbContext(typeof(WmsDbContext))]
    [Migration(Broken)]
    internal sealed class ItBroken : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SELECT * FROM sample.does_not_exist");
        }



        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SELECT 1");
        }
    }
}
