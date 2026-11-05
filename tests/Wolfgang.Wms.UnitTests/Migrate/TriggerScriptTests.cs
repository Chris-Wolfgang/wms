// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wolfgang.Wms.Infrastructure.Database.Conventions;

namespace Wolfgang.Wms.UnitTests.Migrate;

/// <summary>
/// The DBA-review script (E4.2) must run as generated. EF's idempotent SQL Server script wraps every command of a
/// migration in <c>IF NOT EXISTS (...) BEGIN ... END</c>, and SQL Server only accepts <c>CREATE [OR ALTER] TRIGGER</c>
/// as the first statement of its batch, so the trigger DDL has to run as a nested batch (<c>EXEC(N'...')</c>).
/// The PostgreSQL script wraps commands in a <c>DO $EF$ ... $EF$</c> block, where the function and trigger are legal.
/// A migrations assembly of its own (its context is not <see cref="Wolfgang.Wms.Infrastructure.Database.WmsDbContext"/>),
/// so nothing else discovers the sample migration.
/// </summary>
public sealed class TriggerScriptTests
{
    private const string Migration = "20990104000000_SampleTrigger";



    [Fact]
    public void SqlServer_idempotent_script_runs_the_trigger_ddl_as_a_nested_batch()
    {
        using var context = Context(sqlServer: true);
        var migrator = context.GetService<IMigrator>();

        var up = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        var down = migrator.GenerateScript(Migration, Microsoft.EntityFrameworkCore.Migrations.Migration.InitialDatabase, MigrationsSqlGenerationOptions.Idempotent);

        var guardAt = up.IndexOf("IF NOT EXISTS", StringComparison.Ordinal);
        var execAt = up.IndexOf("EXEC(N'CREATE OR ALTER TRIGGER [sample].[trg_note_row_version] ON [sample].[note] AFTER UPDATE AS", StringComparison.Ordinal);
        var endAt = up.IndexOf("END');", execAt, StringComparison.Ordinal);
        Assert.InRange(guardAt, 0, execAt - 1);
        Assert.InRange(endAt, execAt, up.Length);
        Assert.DoesNotMatch(@"(?m)^\s*CREATE OR ALTER TRIGGER", up);   // never a bare statement inside the IF ... BEGIN ... END block
        Assert.Contains("DROP TRIGGER IF EXISTS [sample].[trg_note_row_version];", down, StringComparison.Ordinal);
    }



    [Fact]
    public void PostgreSql_idempotent_script_keeps_the_function_and_trigger_inside_the_do_block()
    {
        using var context = Context(sqlServer: false);
        var migrator = context.GetService<IMigrator>();

        var up = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        var down = migrator.GenerateScript(Migration, Microsoft.EntityFrameworkCore.Migrations.Migration.InitialDatabase, MigrationsSqlGenerationOptions.Idempotent);

        var blockAt = up.IndexOf("DO $EF$", StringComparison.Ordinal);
        var functionAt = up.IndexOf("CREATE OR REPLACE FUNCTION wms.set_row_version()", StringComparison.Ordinal);
        var triggerAt = up.IndexOf("CREATE TRIGGER trg_note_row_version BEFORE UPDATE ON sample.note", StringComparison.Ordinal);
        var blockEndAt = up.IndexOf("END $EF$;", triggerAt, StringComparison.Ordinal);
        Assert.InRange(blockAt, 0, functionAt - 1);
        Assert.InRange(functionAt, 0, triggerAt - 1);
        Assert.InRange(blockEndAt, triggerAt, up.Length);
        Assert.Contains("DROP TRIGGER IF EXISTS trg_note_row_version ON sample.note;", down, StringComparison.Ordinal);
        Assert.Contains("DO $wms$ BEGIN", down, StringComparison.Ordinal);   // the function-drop block nests inside the script's DO $EF$ block
    }



    private static TriggerSampleDbContext Context(bool sqlServer)
    {
        var assembly = typeof(TriggerScriptTests).Assembly.GetName().Name;
        var builder = new DbContextOptionsBuilder<TriggerSampleDbContext>();
        if (sqlServer)
        {
            builder.UseSqlServer(TestConnectionStrings.SqlServer("nowhere"), sql => sql.MigrationsAssembly(assembly));
        }
        else
        {
            builder.UseNpgsql(TestConnectionStrings.PostgreSql("nowhere"), npgsql => npgsql.MigrationsAssembly(assembly));
        }

        return new TriggerSampleDbContext(builder.Options);
    }



    internal sealed class TriggerSampleDbContext(DbContextOptions<TriggerSampleDbContext> options) : DbContext(options);



    [DbContext(typeof(TriggerSampleDbContext))]
    [Migration(Migration)]
    internal sealed class SampleTrigger : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            RowVersioning.AddUpdateTrigger(migrationBuilder, "sample", "note");
        }



        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RowVersioning.DropUpdateTrigger(migrationBuilder, "sample", "note");
        }
    }
}
