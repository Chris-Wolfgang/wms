// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

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

        var script = runner.Script(null, null);
        var delta = runner.Script("Initial", "Initial");

        Assert.Contains(idempotentMarker, script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(historyTable, script, StringComparison.Ordinal);
        Assert.Contains("_Initial", script, StringComparison.Ordinal);
        Assert.DoesNotContain("_Initial", delta.Replace("-- Downgrade", string.Empty, StringComparison.Ordinal).Split('\n').Where(l => l.Contains("INSERT", StringComparison.OrdinalIgnoreCase)).DefaultIfEmpty(string.Empty).First(), StringComparison.Ordinal);
    }



    [Fact]
    public void A_downgrade_script_carries_the_direction_header()
    {
        using var context = Context("PostgreSql");
        var runner = new MigrationRunner(context);

        var script = runner.Script("Initial", MigrationRunner.Empty);

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
        var badTarget = await MigrateProgram.RunAsync(["--script", "--to", "Nope"], output, error, Configuration("PostgreSql", "Host=x"), CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitOk, help);
        Assert.Equal(MigrateProgram.ExitUsage, usage);
        Assert.Equal(MigrateProgram.ExitUsage, noProvider);
        Assert.Equal(MigrateProgram.ExitUsage, badTarget);
        Assert.Contains("wms-migrate [migrate] [options]", output.ToString(), StringComparison.Ordinal);
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
            var toStdout = await MigrateProgram.RunAsync(["--script", "--provider", "SqlServer", "--connection-string", "Server=nowhere"], output, TextWriter.Null, Configuration("None", null), CancellationToken.None);
            var toFile = await MigrateProgram.RunAsync(["--script", "--output", file], output, TextWriter.Null, Configuration("PostgreSql", "Host=nowhere"), CancellationToken.None);

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

        var status = await MigrateProgram.RunAsync(["--status"], output, error, Configuration("SqlServer", "Server=127.0.0.1,1;Database=wms;User Id=x;Password=x;Encrypt=False;Connect Timeout=1;Connect Retry Count=0"), CancellationToken.None);
        var fromWorkingDirectory = await MigrateProgram.RunAsync(["--status"], output, error, configuration: null, CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitOk, status);
        Assert.Contains("Reachable: no", output.ToString(), StringComparison.Ordinal);
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



    private static WmsDbContext Context(string provider)
    {
        var builder = new DbContextOptionsBuilder<WmsDbContext>();
        DatabaseServiceCollectionExtensions.Configure(builder, new DatabaseOptions { Provider = provider, ConnectionString = "Server=nowhere;Host=nowhere" });
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
        }
    }



    private sealed class HarmlessSampleMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SELECT 1");
        }
    }
}
