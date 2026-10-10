// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Migrate;

namespace Wolfgang.Wms.UnitTests.Migrate;

/// <summary>
/// E4.2 / E4.6 without a database: target resolution, destructive-step detection, idempotent scripts per
/// provider, and the tool's usage and configuration exit codes.
/// </summary>
public sealed class MigrationRunnerTests
{
    [Fact]
    public void Resolve_accepts_ids_names_timestamp_prefixes_latest_and_empty()
    {
        string[] shipped = ["20260920025830_Initial", "20261001120000_AddZones"];

        Assert.Equal("20261001120000_AddZones", MigrationRunner.Resolve(shipped, null));
        Assert.Equal("20261001120000_AddZones", MigrationRunner.Resolve(shipped, "latest"));
        Assert.Null(MigrationRunner.Resolve(shipped, MigrationRunner.Empty));
        Assert.Equal("20260920025830_Initial", MigrationRunner.Resolve(shipped, "20260920025830_Initial"));
        Assert.Equal("20260920025830_Initial", MigrationRunner.Resolve(shipped, "Initial"));
        Assert.Equal("20261001120000_AddZones", MigrationRunner.Resolve(shipped, "20261001"));
        Assert.Throws<ArgumentException>(() => MigrationRunner.Resolve(shipped, "Nope"));
        Assert.Throws<ArgumentNullException>(() => MigrationRunner.Resolve(null!, "x"));
    }



    [Fact]
    public void Resolve_rejects_a_prefix_or_name_that_matches_more_than_one_migration_and_lists_the_matches()
    {
        string[] shipped = ["20261001090000_Zones", "20261001120000_AddBins", "20261002080000_Add_Zones"];

        var prefix = Assert.Throws<ArgumentException>(() => MigrationRunner.Resolve(shipped, "20261001"));
        var name = Assert.Throws<ArgumentException>(() => MigrationRunner.Resolve(shipped, "Zones"));

        Assert.Contains("'20261001' is ambiguous; it matches 20261001090000_Zones, 20261001120000_AddBins.", prefix.Message, StringComparison.Ordinal);
        Assert.Contains("'Zones' is ambiguous; it matches 20261001090000_Zones, 20261002080000_Add_Zones.", name.Message, StringComparison.Ordinal);
        Assert.Equal("20261001120000_AddBins", MigrationRunner.Resolve(shipped, "AddBins"));
        Assert.Equal("20261001120000_AddBins", MigrationRunner.Resolve(shipped, "2026100112"));
        Assert.Equal("20261002080000_Add_Zones", MigrationRunner.Resolve(shipped, "Add_Zones"));
    }



    [Fact]
    public void Destructive_steps_are_dropped_tables_columns_schemas_and_deleted_rows()
    {
        var steps = MigrationRunner.DestructiveOperationsIn(new DestructiveSampleMigration());

        Assert.Equal
        (
            [
                "drop table picking.container",
                "drop column picking.zone_group.name",
                "drop schema layout",
                "delete rows from picking.tote",
                "raw SQL, not inspected: SELECT 1",
                "raw SQL, not inspected: DELETE FROM picking.tote WHERE created < now() - interval '1 year'; TRUNCATE pic...",
            ],
            steps
        );
        Assert.Empty(MigrationRunner.DestructiveOperationsIn(new HarmlessSampleMigration()));
        Assert.Empty(new DestructiveSampleMigration().UpOperations);
        Assert.Empty(new HarmlessSampleMigration().UpOperations);
        Assert.Throws<ArgumentNullException>(() => MigrationRunner.DestructiveOperationsIn(null!));
    }



    [Theory]
    [InlineData("SqlServer", "IF NOT EXISTS", "[wms].[migrations_history]")]
    [InlineData("PostgreSql", "IF NOT EXISTS", "wms.migrations_history")]
    public void Script_is_idempotent_and_provider_specific_without_a_connection(string provider, string idempotentMarker, string historyTable)
    {
        using var context = Context(provider);
        var runner = new MigrationRunner(context);

        var full = runner.Script(null, null, confirmDataLoss: false);
        var none = runner.Script("Initial", "Initial", confirmDataLoss: false);
        var script = full.Sql!;
        var delta = none.Sql!;

        Assert.Equal(MigrationDirection.Up, full.Direction);
        Assert.Equal(MigrationDirection.None, none.Direction);
        Assert.False(full.RequiresConfirmation);

        Assert.Contains(idempotentMarker, script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(historyTable, script, StringComparison.Ordinal);
        Assert.Contains("_Initial", script, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO " + historyTable, script, StringComparison.OrdinalIgnoreCase);        // the upgrade records each migration it applies
        Assert.DoesNotContain("INSERT INTO " + historyTable, delta, StringComparison.OrdinalIgnoreCase);   // Initial -> Initial applies nothing, so it writes no history row
        Assert.DoesNotContain("_Initial", delta, StringComparison.Ordinal);
    }



    [Fact]
    public void A_downgrade_script_carries_the_direction_header()
    {
        using var context = Context("PostgreSql");
        var runner = new MigrationRunner(context);

        var downgrade = runner.Script("Initial", MigrationRunner.Empty, confirmDataLoss: false);
        var script = downgrade.Sql!;

        Assert.Equal(MigrationDirection.Down, downgrade.Direction);
        Assert.Empty(downgrade.DestructiveSteps);
        Assert.StartsWith("-- Downgrade from 20260920025835_Initial to 0", script, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM wms.migrations_history", script, StringComparison.OrdinalIgnoreCase);
    }



    [Fact]
    public async Task Help_and_usage_errors_exit_without_touching_a_database()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var help = await MigrateProgram.RunAsync(["--help"], output, error, Configuration("None", null), CancellationToken.None);
        var usage = await MigrateProgram.RunAsync(["--up"], output, error, Configuration("None", null), CancellationToken.None);
        var noProvider = await MigrateProgram.RunAsync([], output, error, Configuration("None", null), CancellationToken.None);
        var badTarget = await MigrateProgram.RunAsync(["--script", "--to", "Nope"], output, error, Configuration("PostgreSql", TestConnectionStrings.PostgreSql("x")), CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitOk, help);
        Assert.Equal(MigrateProgram.ExitUsage, usage);
        Assert.Equal(MigrateProgram.ExitUsage, noProvider);
        Assert.Equal(MigrateProgram.ExitUsage, badTarget);
        Assert.Contains("wms-migrate [options]", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Unknown argument '--up'.", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("must be SqlServer or PostgreSql", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("'Nope' is not a shipped migration", error.ToString(), StringComparison.Ordinal);
    }



    [Fact]
    public async Task Script_mode_writes_to_standard_output_or_a_file_without_a_connection()
    {
        var output = new StringWriter();
        var file = Path.Combine(Path.GetTempPath(), "wms-migrate-" + Guid.NewGuid().ToString("N") + ".sql");
        try
        {
            var toStdout = await MigrateProgram.RunAsync(["--script", "--provider", "SqlServer", "--connection-string", TestConnectionStrings.SqlServer("nowhere")], output, TextWriter.Null, Configuration("None", null), CancellationToken.None);
            var toFile = await MigrateProgram.RunAsync(["--script", "--output", file], output, TextWriter.Null, Configuration("PostgreSql", TestConnectionStrings.PostgreSql("nowhere")), CancellationToken.None);

            Assert.Equal(MigrateProgram.ExitOk, toStdout);
            Assert.Equal(MigrateProgram.ExitOk, toFile);
            Assert.Contains("[wms].[migrations_history]", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("wms.migrations_history", await File.ReadAllTextAsync(file), StringComparison.Ordinal);
            Assert.Contains("Wrote " + file, output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(file);
        }
    }



    [Fact]
    public async Task Status_against_an_unreachable_server_reports_it_and_the_default_configuration_is_read_from_the_working_directory()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var status = await MigrateProgram.RunAsync(["--status"], output, error, Configuration("SqlServer", UnreachableSqlServer), CancellationToken.None);
        var fromWorkingDirectory = await MigrateProgram.RunAsync(["--status"], output, error, configuration: null, CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitOk, status);
        Assert.Contains("Reachable: no: ", output.ToString(), StringComparison.Ordinal);
        Assert.Matches(@"Pending \([1-9]\d*\)", output.ToString());
        Assert.Contains("Schema is not up to date.", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitUsage, fromWorkingDirectory);
    }



    [Fact]
    public async Task RunAsync_rejects_null_arguments()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => MigrateProgram.RunAsync(null!, TextWriter.Null, TextWriter.Null, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => MigrateProgram.RunAsync([], null!, TextWriter.Null, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => MigrateProgram.RunAsync([], TextWriter.Null, null!, null, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => new MigrationRunner(null!));
    }



    [Theory]
    [InlineData("SqlServer", "IF SCHEMA_ID(N'wms') IS NULL EXEC(N'CREATE SCHEMA [wms];');", "CREATE TABLE [wms].[migrations_history]")]
    [InlineData("PostgreSql", "CREATE SCHEMA wms;", "CREATE TABLE IF NOT EXISTS wms.migrations_history")]
    public void The_wms_schema_is_created_before_the_history_table_and_before_any_migration(string provider, string createSchema, string createHistory)
    {
        using var context = Context(provider);

        var script = new MigrationRunner(context).Script(null, null, confirmDataLoss: false).Sql!;
        var schemaAt = script.IndexOf(createSchema, StringComparison.Ordinal);
        var historyAt = script.IndexOf(createHistory, StringComparison.Ordinal);
        var firstMigrationAt = script.IndexOf("_Initial", StringComparison.Ordinal);

        Assert.InRange(schemaAt, 0, historyAt - 1);
        Assert.InRange(historyAt, 0, firstMigrationAt - 1);
    }



    [Fact]
    public void A_data_losing_downgrade_script_needs_confirmation_and_then_lists_every_step_in_its_header()
    {
        using var context = SampleMigrations.Context();
        var runner = new MigrationRunner(context);

        var refused = runner.Script(SampleMigrations.Drops, MigrationRunner.Empty, confirmDataLoss: false);
        var confirmed = runner.Script(SampleMigrations.Drops, MigrationRunner.Empty, confirmDataLoss: true);
        var upgrade = runner.Script(null, null, confirmDataLoss: false);
        string[] expectedSteps =
        [
            SampleMigrations.Drops + ": drop column sample.note.size",
            SampleMigrations.RawSql + ": raw SQL, not inspected: DELETE FROM sample.note; DROP TABLE sample.note",
        ];

        Assert.True(refused.RequiresConfirmation);
        Assert.Null(refused.Sql);
        Assert.Equal(MigrationDirection.Down, refused.Direction);
        Assert.Equal(expectedSteps, refused.DestructiveSteps);
        Assert.False(confirmed.RequiresConfirmation);
        Assert.Equal(expectedSteps, confirmed.DestructiveSteps);
        Assert.Equal
        (
            [
                "-- Downgrade from " + SampleMigrations.Drops + " to 0",
                "-- DATA LOSS " + expectedSteps[0],
                "-- DATA LOSS " + expectedSteps[1],
            ],
            confirmed.Sql!.Split(Environment.NewLine).Take(3)
        );
        Assert.Contains("DROP TABLE sample.note", confirmed.Sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE sample.note", upgrade.Sql, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE sample.note ADD size integer", upgrade.Sql, StringComparison.Ordinal);
    }



    [Fact]
    public async Task Script_mode_refuses_a_data_losing_downgrade_without_confirm_data_loss_and_writes_nothing()
    {
        using var context = SampleMigrations.Context();
        var runner = new MigrationRunner(context);
        var refusedOutput = new StringWriter();
        var refusedError = new StringWriter();
        var confirmedOutput = new StringWriter();
        var file = Path.Combine(Path.GetTempPath(), "wms-migrate-" + Guid.NewGuid().ToString("N") + ".sql");

        var refused = await MigrateProgram.ScriptAsync(runner, MigrateCommandLine.Parse(["--script", "--from", SampleMigrations.Drops, "--to", "0", "--output", file]), refusedOutput, refusedError, CancellationToken.None);
        var confirmed = await MigrateProgram.ScriptAsync(runner, MigrateCommandLine.Parse(["--script", "--from", SampleMigrations.Drops, "--to", "0", "--confirm-data-loss"]), confirmedOutput, TextWriter.Null, CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitConfirmationRequired, refused);
        Assert.False(File.Exists(file));
        Assert.Empty(refusedOutput.ToString());
        Assert.Contains("This downgrade script loses data; re-run with --confirm-data-loss to proceed:", refusedError.ToString(), StringComparison.Ordinal);
        Assert.Contains("  " + SampleMigrations.Drops + ": drop column sample.note.size", refusedError.ToString(), StringComparison.Ordinal);
        Assert.Contains("raw SQL, not inspected", refusedError.ToString(), StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitOk, confirmed);
        Assert.StartsWith("-- Downgrade from " + SampleMigrations.Drops, confirmedOutput.ToString(), StringComparison.Ordinal);
        await Assert.ThrowsAsync<ArgumentNullException>(() => MigrateProgram.ScriptAsync(null!, MigrateCommandLine.Parse([]), TextWriter.Null, TextWriter.Null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => MigrateProgram.ScriptAsync(runner, null!, TextWriter.Null, TextWriter.Null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => MigrateProgram.ScriptAsync(runner, MigrateCommandLine.Parse([]), null!, TextWriter.Null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => MigrateProgram.ScriptAsync(runner, MigrateCommandLine.Parse([]), TextWriter.Null, null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => MigrateProgram.ApplyAsync(null!, MigrateCommandLine.Parse([]), TextWriter.Null, TextWriter.Null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => MigrateProgram.ApplyAsync(runner, null!, TextWriter.Null, TextWriter.Null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => MigrateProgram.ApplyAsync(runner, MigrateCommandLine.Parse([]), null!, TextWriter.Null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => MigrateProgram.ApplyAsync(runner, MigrateCommandLine.Parse([]), TextWriter.Null, null!, CancellationToken.None));
    }



    [Theory]
    [InlineData("SqlServer", "[wms].[migrations_history]")]
    [InlineData("PostgreSql", "wms.migrations_history")]
    public async Task Script_mode_needs_no_connection_string(string provider, string historyTable)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await MigrateProgram.RunAsync(["--script", "--provider", provider], output, error, Configuration("None", null), CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitOk, code);
        Assert.Empty(error.ToString());
        Assert.Contains(historyTable, output.ToString(), StringComparison.Ordinal);
    }



    [Fact]
    public async Task Script_mode_still_validates_the_provider_and_the_certificate_switch()
    {
        var error = new StringWriter();

        var badProvider = await MigrateProgram.RunAsync(["--script", "--provider", "Oracle"], TextWriter.Null, error, Configuration("None", null), CancellationToken.None);
        var noProvider = await MigrateProgram.RunAsync(["--script"], TextWriter.Null, error, Configuration("None", null), CancellationToken.None);
        var certificateOnPostgreSql = await MigrateProgram.RunAsync(["--script", "--provider", "PostgreSql", "--trust-server-certificate"], TextWriter.Null, error, Configuration("None", null), CancellationToken.None);
        var applyWithoutConnection = await MigrateProgram.RunAsync(["--provider", "PostgreSql"], TextWriter.Null, error, Configuration("None", null), CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitUsage, badProvider);
        Assert.Equal(MigrateProgram.ExitUsage, noProvider);
        Assert.Equal(MigrateProgram.ExitUsage, certificateOnPostgreSql);
        Assert.Equal(MigrateProgram.ExitUsage, applyWithoutConnection);
        Assert.Contains("Provider must be one of None, SqlServer or PostgreSql; got 'Oracle'.", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("must be SqlServer or PostgreSql", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("TrustServerCertificate applies to SqlServer only", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("ConnectionString is required when Wms:Database:Provider is PostgreSql", error.ToString(), StringComparison.Ordinal);
    }



    [Fact]
    public async Task A_malformed_connection_string_is_a_configuration_error_not_an_unhandled_exception()
    {
        var error = new StringWriter();

        var code = await MigrateProgram.RunAsync(["--status", "--trust-server-certificate"], TextWriter.Null, error, Configuration("SqlServer", "Server=x;Encrypt=True;this is not a keyword value pair"), CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitUsage, code);
        Assert.StartsWith("Wms:Database:ConnectionString is not valid: ", error.ToString(), StringComparison.Ordinal);
    }



    [Fact]
    public async Task Configuration_that_does_not_load_or_bind_is_a_configuration_error_not_an_unhandled_exception()
    {
        var error = new StringWriter();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Wms:Database:Provider"] = "SqlServer",
                ["Wms:Database:TrustServerCertificate"] = "maybe",
            })
            .Build();

        var code = await MigrateProgram.RunAsync(["--status"], TextWriter.Null, error, configuration, CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitUsage, code);
        Assert.StartsWith("Configuration is not valid: ", error.ToString(), StringComparison.Ordinal);
    }



    [Fact]
    public async Task An_unreachable_database_is_never_reported_as_already_at_the_target()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await MigrateProgram.RunAsync(["--to", "0"], output, error, Configuration("SqlServer", UnreachableSqlServer), CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitRefused, code);
        Assert.DoesNotContain("Nothing to do.", output.ToString(), StringComparison.Ordinal);
        Assert.StartsWith("Nothing was applied. Wms:Database: the database cannot be reached;", error.ToString(), StringComparison.Ordinal);
    }



    [Fact]
    public void Refusal_names_an_unreachable_database_and_a_schema_newer_than_the_build()
    {
        var unreachable = new MigrationStatus(Reachable: false, Applied: [], Pending: ["20260920025830_Initial"], Expected: "20260920025830_Initial") { Error = "connection refused" };
        var newer = new MigrationStatus(Reachable: true, Applied: ["20260920025830_Initial", "20991231000000_FromTheFuture"], Pending: [], Expected: "20260920025830_Initial")
        {
            Unknown = ["20991231000000_FromTheFuture"],
        };
        var behind = new MigrationStatus(Reachable: true, Applied: [], Pending: ["20260920025830_Initial"], Expected: "20260920025830_Initial");

        Assert.Equal("Wms:Database: the database cannot be reached; check the connection string and that the server is up. connection refused", MigrationRunner.Refusal(unreachable));
        Assert.Equal("The database schema is newer than this build (unknown migrations: 20991231000000_FromTheFuture). Upgrade the application, or restore the backup taken before the upgrade.", MigrationRunner.Refusal(newer));
        Assert.Null(MigrationRunner.Refusal(behind));
        Assert.Throws<ArgumentNullException>(() => MigrationRunner.Refusal(null!));
    }



    [Fact]
    public void A_refused_result_is_neither_successful_nor_awaiting_confirmation()
    {
        var refused = new MigrationResult(MigrationDirection.None, From: null, To: null, Steps: [], FailedMigration: null, Error: "unreachable", DestructiveSteps: []);
        var failed = new MigrationResult(MigrationDirection.Up, From: null, To: "x", Steps: [], FailedMigration: "x", Error: "boom", DestructiveSteps: []);

        Assert.True(refused.Refused);
        Assert.False(refused.Succeeded);
        Assert.False(refused.RequiresConfirmation);
        Assert.False(failed.Refused);
        Assert.False(failed.Succeeded);
    }



    private static readonly string UnreachableSqlServer = new SqlConnectionStringBuilder
    {
        DataSource = "127.0.0.1,1",
        InitialCatalog = "wms",
        UserID = "x",
        Password = "x",
        Encrypt = SqlConnectionEncryptOption.Mandatory,
        ConnectTimeout = 1,
        ConnectRetryCount = 0,
    }.ConnectionString;



    private static WmsDbContext Context(string provider)
    {
        var builder = new DbContextOptionsBuilder<WmsDbContext>();
        DatabaseServiceCollectionExtensions.Configure(builder, new DatabaseOptions { Provider = provider, ConnectionString = string.Equals(provider, "SqlServer", StringComparison.Ordinal) ? TestConnectionStrings.SqlServer("nowhere") : TestConnectionStrings.PostgreSql("nowhere") });
        return new WmsDbContext(builder.Options);
    }



    private static IConfiguration Configuration(string provider, string? connectionString)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Wms:Database:Provider"] = provider,
                ["Wms:Database:ConnectionString"] = connectionString,
            })
            .Build();
    }



    private sealed class DestructiveSampleMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("container", "picking");
            migrationBuilder.DropColumn("name", "zone_group", "picking");
            migrationBuilder.DropSchema("layout");
            migrationBuilder.DeleteData("tote", "id", 1L, "picking");
            migrationBuilder.Sql("SELECT 1");
            migrationBuilder.Sql("DELETE FROM picking.tote\r\n  WHERE created < now() - interval '1 year';\n\tTRUNCATE picking.container_history;");
        }
    }



    private sealed class HarmlessSampleMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("ix_tote_code", "tote", "picking");
        }
    }
}
