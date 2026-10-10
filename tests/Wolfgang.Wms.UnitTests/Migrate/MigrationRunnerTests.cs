// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.RegularExpressions;
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
    public void Destructive_steps_are_dropped_tables_columns_schemas_sequences_rows_changed_and_columns_narrowed()
    {
        var steps = MigrationRunner.DestructiveOperationsIn(new DestructiveSampleMigration());

        Assert.Equal
        (
            [
                "drop table picking.container",
                "drop column picking.zone_group.name",
                "drop schema layout",
                "drop sequence wms.row_version_seq",
                "restart sequence wms.row_version_seq at 1",
                "delete rows from picking.tote",
                "update rows in picking.tote",
                "narrow column picking.tote.code (max length 50 -> 20)",
                "narrow column picking.tote.weight (precision 18 -> 9, scale 4 -> 2, integral digits 14 -> 7)",
                "narrow column picking.tote.label (type nvarchar(max) -> int)",
                "narrow column picking.tote.name (type inferred for String -> varchar(50))",
                "narrow column picking.tote.amount (integral digits 16 -> 14)",
                "narrow column picking.tote.remark (max length unbounded -> 20)",
                "narrow column picking.tote.price (precision unbounded -> 9)",
                "narrow column picking.tote.title (unicode -> non-unicode)",
                "narrow column picking.tote.total (integral digits 18 -> 14)",
                "narrow column picking.tote.memo (max length 50 -> provider default)",
                "narrow column picking.tote.fee (precision 18 -> provider default)",
                "narrow column picking.tote.total (precision 9 -> provider default)",
                "narrow column picking.tote.seen (precision 6 -> 3)",
                "narrow column picking.tote.state (type \"Order State\" -> \"OrderState\")",
                "narrow column picking.tote.fee (precision 18 -> provider default)",
                "narrow column picking.tote.stamp (precision 7 -> 3)",
                "narrow column picking.tote.seen (precision 6 -> 3)",
                "narrow column picking.tote.state (type \"Order(TypeA)\" -> \"Order(TypeB)\")",
                "narrow column picking.tote.flags (precision 8 -> 1)",
                "narrow column picking.tote.rounded (integral digits 5 -> 4)",
                "narrow column picking.tote.blob (precision 8 -> provider default)",
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
        var historyInsert = new Regex(@"INSERT\s+INTO\s+" + Regex.Escape(historyTable), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.Matches(historyInsert, script);        // the upgrade records each migration it applies
        Assert.DoesNotMatch(historyInsert, delta);    // Initial -> Initial applies nothing, so it writes no history row
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
    public async Task Status_against_an_unreachable_server_reports_it()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var status = await MigrateProgram.RunAsync(["--status"], output, error, configuration: Configuration("SqlServer", UnreachableSqlServer), cancellationToken: CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitOk, status);
        Assert.Contains("Reachable: no: ", output.ToString(), StringComparison.Ordinal);
        Assert.Matches(@"Pending \([1-9]\d*\)", output.ToString());
        Assert.Contains("Schema is not up to date.", output.ToString(), StringComparison.Ordinal);
    }



    [Fact]
    public async Task Status_with_an_empty_configuration_is_a_usage_error_naming_the_provider_setting()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        // An explicit empty configuration, never `null`: null makes the tool read appsettings.json in the working
        // directory and the process environment, and a developer box or runner that has Wms__Database__* set would
        // either fail this assertion or connect to whatever database it names.
        var code = await MigrateProgram.RunAsync(["--status"], output, error, configuration: new ConfigurationBuilder().Build(), cancellationToken: CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitUsage, code);
        Assert.Contains("Wms:Database:Provider", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, output.ToString());
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
            migrationBuilder.DropSequence("row_version_seq", "wms");
            migrationBuilder.RestartSequence("row_version_seq", startValue: 1L, schema: "wms");   // the current value is every client's sync watermark; a restart discards it
            migrationBuilder.DeleteData("tote", "id", 1L, "picking");
            migrationBuilder.UpdateData("tote", "id", 1L, "code", "T-1", "picking");
            migrationBuilder.AlterColumn<string>("code", "tote", maxLength: 20, schema: "picking", oldMaxLength: 50);
            migrationBuilder.AlterColumn<decimal>("weight", "tote", precision: 9, scale: 2, schema: "picking", oldPrecision: 18, oldScale: 4);
            migrationBuilder.AlterColumn<int>("label", "tote", type: "int", schema: "picking", oldClrType: typeof(string), oldType: "nvarchar(max)");
            migrationBuilder.AlterColumn<string>("name", "tote", type: "varchar(50)", maxLength: 50, schema: "picking", oldMaxLength: 50);   // explicit store type where the old one was inferred (nvarchar -> varchar loses characters)
            migrationBuilder.AlterColumn<decimal>("amount", "tote", type: "decimal(18,4)", schema: "picking", oldType: "decimal(18,2)");   // same precision, more scale: two integral digits fewer
            migrationBuilder.AlterColumn<string>("remark", "tote", type: "nvarchar(20)", schema: "picking", oldType: "nvarchar(max)");   // facets in the store type only, as EF scaffolds them
            migrationBuilder.AlterColumn<decimal>("price", "tote", type: "numeric(9,2)", schema: "picking", oldType: "numeric");   // PostgreSQL unbounded numeric bounded to 9 digits
            migrationBuilder.AlterColumn<string>("title", "tote", unicode: false, schema: "picking", oldUnicode: true);   // nvarchar -> varchar with no store type written
            migrationBuilder.AlterColumn<decimal>("total", "tote", type: "decimal(18,4)", schema: "picking", oldType: "decimal(18)");   // decimal(18) is precision 18, scale 0: four integral digits fewer
            migrationBuilder.AlterColumn<string>("memo", "tote", type: "varchar", schema: "picking", oldType: "varchar(50)");   // SQL Server reads a bare varchar as varchar(1)
            migrationBuilder.AlterColumn<decimal>("fee", "tote", type: "decimal", schema: "picking", oldType: "decimal(18,2)");   // SQL Server reads a bare decimal as decimal(18,0)
            migrationBuilder.AlterColumn<decimal>("total", "tote", type: "numeric", schema: "picking", oldType: "numeric(9,2)");   // unbounded on PostgreSQL, numeric(18,0) on SQL Server: the provider decides, so confirm
            migrationBuilder.AlterColumn<DateTimeOffset>("seen", "tote", type: "timestamp(3) with time zone", schema: "picking", oldType: "timestamp(6) with time zone");   // the qualifier after the facet is part of the type; fewer fractional digits
            migrationBuilder.AlterColumn<string>("state", "tote", type: "\"OrderState\"", schema: "picking", oldType: "\"Order State\"");   // two user-defined types whose names differ by a space
            migrationBuilder.AlterColumn<decimal>("fee", "tote", schema: "picking", oldPrecision: 18, oldScale: 4);   // no store type or facets written: the provider's default decimal, decimal(18,2) on SQL Server
            migrationBuilder.AlterColumn<DateTime>("stamp", "tote", type: "datetime2(3)", schema: "picking", oldType: "datetime2");   // SQL Server's bare datetime2 is datetime2(7)
            migrationBuilder.AlterColumn<DateTimeOffset>("seen", "tote", type: "timestamp(3) with time zone", schema: "picking", oldType: "timestamp with time zone");   // PostgreSQL's bare timestamp is timestamp(6)
            migrationBuilder.AlterColumn<string>("state", "tote", type: "\"Order(TypeB)\"", schema: "picking", oldType: "\"Order(TypeA)\"");   // parentheses inside a quoted name are the name, not facets
            migrationBuilder.AlterColumn<byte[]>("flags", "tote", type: "bit", schema: "picking", oldType: "bit(8)");   // PostgreSQL's bare bit is bit(1)
            migrationBuilder.AlterColumn<decimal>("rounded", "tote", type: "numeric(2,-2)", schema: "picking", oldType: "numeric(2,-3)");   // a negative scale: 5 integral digits become 4
            migrationBuilder.AlterColumn<byte[]>("blob", "tote", type: "geometry", schema: "picking", oldType: "geometry(8)");   // a family this code does not know: the default may be narrower, so confirm
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
            migrationBuilder.AlterColumn<string>("code", "tote", maxLength: 100, schema: "picking", oldMaxLength: 50);   // widening keeps every value
            migrationBuilder.AlterColumn<string>("note", "tote", nullable: true, schema: "picking", oldNullable: false);
            migrationBuilder.AlterColumn<decimal>("weight", "tote", precision: 18, scale: 4, schema: "picking", oldPrecision: 9, oldScale: 2);
            migrationBuilder.AlterColumn<decimal>("weight", "tote", type: "decimal(9, 3)", schema: "picking", oldType: "decimal(9,3)");   // same store type, different spacing
            migrationBuilder.AlterColumn<string>("name", "tote", type: "nvarchar(100)", schema: "picking", oldType: "nvarchar(50)");   // typed widening, as EF scaffolds it
            migrationBuilder.AlterColumn<decimal>("weight", "tote", type: "decimal(18,4)", schema: "picking", oldType: "decimal(9,2)");   // typed widening: precision, scale and integral digits all grow
            migrationBuilder.AlterColumn<string>("title", "tote", unicode: true, schema: "picking", oldUnicode: false);   // varchar -> nvarchar keeps every character
            migrationBuilder.AlterColumn<decimal>("total", "tote", type: "decimal(18,4)", schema: "picking", oldType: "decimal(9)");   // decimal(9) is 9 integral digits; decimal(18,4) keeps 14
            migrationBuilder.AlterColumn<string>("remark", "tote", type: "nvarchar(max)", schema: "picking", oldType: "nvarchar(50)");   // an explicit max is unbounded on both providers
            migrationBuilder.AlterColumn<string>("memo", "tote", type: "varchar", schema: "picking", oldType: "varchar");   // no facet on either side: nothing dropped
            migrationBuilder.AlterColumn<DateTimeOffset>("seen", "tote", type: "timestamp(6) with time zone", schema: "picking", oldType: "timestamp(3) with time zone");   // same type, more fractional digits
            migrationBuilder.AlterColumn<DateTimeOffset>("seen", "tote", type: "TIMESTAMP(3) WITH TIME ZONE", schema: "picking", oldType: "timestamp(3) with time zone");   // keyword case is formatting
            migrationBuilder.AlterColumn<string>("state", "tote", type: "\"OrderState\"", schema: "picking", oldType: "\"OrderState\"");   // the same user-defined type
            migrationBuilder.AlterColumn<string>("state", "tote", type: "\"Order(TypeA)\"", schema: "picking", oldType: "\"Order(TypeA)\"");   // the same user-defined type, parentheses and all
            migrationBuilder.AlterColumn<DateTime>("stamp", "tote", type: "datetime2(7)", schema: "picking", oldType: "datetime2");   // the default written out: no change
            migrationBuilder.AlterColumn<DateTimeOffset>("seen", "tote", type: "timestamp with time zone", schema: "picking", oldType: "timestamp(6) with time zone");   // back to the default, which is 6: no change
            migrationBuilder.AlterColumn<byte[]>("flags", "tote", type: "bit(8)", schema: "picking", oldType: "bit");   // bit(1) -> bit(8) keeps every value
            migrationBuilder.AlterColumn<decimal>("total", "tote", type: "numeric(18,4)", schema: "picking", oldType: "decimal(9,2)");   // numeric is decimal on both providers: a widening, not a conversion
        }
    }
}
