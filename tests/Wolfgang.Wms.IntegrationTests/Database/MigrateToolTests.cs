// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Migrate;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E4.1 / E4.3 / E4.5 / E4.6 against real engines: <c>wms-migrate</c> reports a never-migrated database as
/// reachable with nothing applied, installs the schema into it (history table in schema <c>wms</c>), reports
/// status, goes down to an empty schema and back up (the up → down → up run CI requires on both providers), and
/// refuses to touch a schema a newer build migrated, all through the tool's own entry point. A database that
/// does not exist is reported as unreachable and never created (E4.5: the DBA creates it, the tool fills it).
/// </summary>
public sealed class MigrateToolTests
{
    [SqlServerFact]
    public async Task SqlServer_up_status_down_up_through_the_tool()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();

        await AssertUpDownUpAsync("SqlServer", database.ConnectionString, ["--trust-server-certificate"]);
    }



    [DockerFact]
    public async Task PostgreSql_up_status_down_up_through_the_tool()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertUpDownUpAsync("PostgreSql", container.GetConnectionString(), []);
    }



    [SqlServerFact]
    public async Task SqlServer_a_database_that_does_not_exist_is_reported_and_never_created()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();
        var missing = "wms_missing_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = new SqlConnectionStringBuilder(database.ConnectionString) { InitialCatalog = missing }.ConnectionString;

        await AssertMissingDatabaseIsRefusedAsync("SqlServer", connectionString, missing, ["--trust-server-certificate"]);

        await using var server = new SqlConnection(database.ConnectionString);
        await server.OpenAsync();
        await using var exists = server.CreateCommand();
        exists.CommandText = "SELECT DB_ID(@name)";
        exists.Parameters.AddWithValue("@name", missing);
        Assert.Equal(DBNull.Value, await exists.ExecuteScalarAsync());
    }



    [DockerFact]
    public async Task PostgreSql_a_database_that_does_not_exist_is_reported_and_never_created()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();
        var missing = "wms_missing_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = missing }.ConnectionString;

        await AssertMissingDatabaseIsRefusedAsync("PostgreSql", connectionString, missing, []);

        await using var server = new NpgsqlConnection(container.GetConnectionString());
        await server.OpenAsync();
        await using var exists = server.CreateCommand();
        exists.CommandText = "SELECT count(*) FROM pg_database WHERE datname = @name";
        exists.Parameters.AddWithValue("name", missing);
        Assert.Equal(0L, await exists.ExecuteScalarAsync());
    }



    private static async Task AssertMissingDatabaseIsRefusedAsync(string provider, string connectionString, string missing, string[] extra)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Wms:Database:Provider"] = provider,
                ["Wms:Database:ConnectionString"] = connectionString,
            })
            .Build();

        var status = await RunAsync(configuration, ["--status", .. extra]);
        var apply = await RunAsync(configuration, extra);

        Assert.Equal(MigrateProgram.ExitRefused, status.Code);
        Assert.Contains("Reachable: no: database '" + missing + "' does not exist on the server", status.Output, StringComparison.Ordinal);
        Assert.Contains("Pending: unknown (the database cannot be queried", status.Output, StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitRefused, apply.Code);
        Assert.Contains("does not exist on the server", apply.Error, StringComparison.Ordinal);
    }



    private static async Task AssertUpDownUpAsync(string provider, string connectionString, string[] extra)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Wms:Database:Provider"] = provider,
                ["Wms:Database:ConnectionString"] = connectionString,
            })
            .Build();

        var empty = await RunAsync(configuration, ["--status", .. extra]);
        var up = await RunAsync(configuration, extra);
        var status = await RunAsync(configuration, ["--status", .. extra]);
        var again = await RunAsync(configuration, extra);
        var refused = await RunAsync(configuration, ["--to", "0", .. extra]);   // reverting everything drops wms.row_version_seq: data loss
        var down = await RunAsync(configuration, ["--to", "0", "--confirm-data-loss", .. extra]);
        var statusAfterDown = await RunAsync(configuration, ["--status", .. extra]);
        var backUp = await RunAsync(configuration, ["--to", "Initial", .. extra]);
        var latest = await RunAsync(configuration, extra);
        var finalStatus = await RunAsync(configuration, ["--status", .. extra]);

        Assert.Equal(MigrateProgram.ExitSchemaBehind, empty.Code);   // reachable, nothing applied: an installer's cue to run the tool
        Assert.Contains("Reachable: yes", empty.Output, StringComparison.Ordinal);
        Assert.Contains("Applied (0)", empty.Output, StringComparison.Ordinal);
        Assert.Matches(@"Pending \([1-9]\d*\)", empty.Output);
        Assert.Equal(MigrateProgram.ExitOk, up.Code);
        Assert.Contains("Direction: Up", up.Output, StringComparison.Ordinal);
        Assert.Contains("_Initial", up.Output, StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitOk, status.Code);
        Assert.Contains("Schema is up to date.", status.Output, StringComparison.Ordinal);
        Assert.Contains("Pending (0)", status.Output, StringComparison.Ordinal);
        Assert.Contains("Direction: None", again.Output, StringComparison.Ordinal);
        Assert.Contains("Nothing to do.", again.Output, StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitConfirmationRequired, refused.Code);
        Assert.Contains("drop sequence wms.row_version_seq", refused.Error, StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitOk, down.Code);
        Assert.Contains("Direction: Down", down.Output, StringComparison.Ordinal);
        Assert.Matches(@"Reverted \([1-9]\d*\)", down.Output);
        Assert.Equal(MigrateProgram.ExitSchemaBehind, statusAfterDown.Code);
        Assert.Contains("Applied (0)", statusAfterDown.Output, StringComparison.Ordinal);
        Assert.Contains("Schema is not up to date.", statusAfterDown.Output, StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitOk, backUp.Code);
        Assert.Contains("Direction: Up", backUp.Output, StringComparison.Ordinal);
        Assert.Contains("_Initial", backUp.Output, StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitOk, latest.Code);
        Assert.Contains("Schema is up to date.", finalStatus.Output, StringComparison.Ordinal);

        await AssertHistoryTableInWmsSchemaAsync(provider, connectionString);
        await AssertNewerSchemaIsRefusedAsync(provider, connectionString, configuration, extra);
    }



    private static async Task AssertNewerSchemaIsRefusedAsync(string provider, string connectionString, IConfiguration configuration, string[] extra)
    {
        await FutureMigration.RecordAsync(provider, connectionString);

        var status = await RunAsync(configuration, ["--status", .. extra]);
        var apply = await RunAsync(configuration, extra);
        var down = await RunAsync(configuration, ["--to", "0", "--confirm-data-loss", .. extra]);

        Assert.Equal(MigrateProgram.ExitRefused, status.Code);
        Assert.Contains("Unknown to this build (1):", status.Output, StringComparison.Ordinal);
        Assert.Contains(FutureMigration.Id, status.Output, StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitRefused, apply.Code);
        Assert.Contains("Nothing was applied. The database schema is newer than this build (unknown migrations: " + FutureMigration.Id + ")", apply.Error, StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitRefused, down.Code);
        Assert.Contains(FutureMigration.Id, down.Error, StringComparison.Ordinal);
    }



    private static async Task AssertHistoryTableInWmsSchemaAsync(string provider, string connectionString)
    {
        var builder = new DbContextOptionsBuilder<WmsDbContext>();
        DatabaseServiceCollectionExtensions.Configure(builder, new DatabaseOptions { Provider = provider, ConnectionString = connectionString, TrustServerCertificate = string.Equals(provider, "SqlServer", StringComparison.Ordinal) });
        using var context = new WmsDbContext(builder.Options);

        var applied = await context.Database.GetAppliedMigrationsAsync();
        var historyTables = await context.Database
            .SqlQueryRaw<string>("SELECT CAST(table_schema AS varchar(128)) AS \"Value\" FROM information_schema.tables WHERE table_name = 'migrations_history'")
            .ToListAsync();

        Assert.NotEmpty(applied);
        Assert.Equal(["wms"], historyTables);
    }



    private static async Task<(int Code, string Output, string Error)> RunAsync(IConfiguration configuration, string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await MigrateProgram.RunAsync(args, output, error, configuration, CancellationToken.None);
        return (code, output.ToString(), error.ToString());
    }
}
