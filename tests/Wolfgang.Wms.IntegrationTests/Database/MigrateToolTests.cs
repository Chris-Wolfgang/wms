// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Migrate;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E4.1 / E4.3 / E4.5 / E4.6 against real engines: <c>wms-migrate</c> installs the schema into an empty
/// database (history table in schema <c>wms</c>), reports status, goes down to an empty schema and back up
/// (the up → down → up run CI requires on both providers), all through the tool's own entry point.
/// </summary>
public sealed class MigrateToolTests
{
    [DockerFact]
    public async Task SqlServer_up_status_down_up_through_the_tool()
    {
        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync();

        await AssertUpDownUpAsync("SqlServer", container.GetConnectionString(), ["--trust-server-certificate"]);
    }



    [DockerFact]
    public async Task PostgreSql_up_status_down_up_through_the_tool()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertUpDownUpAsync("PostgreSql", container.GetConnectionString(), []);
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

        var up = await RunAsync(configuration, extra);
        var status = await RunAsync(configuration, ["--status", .. extra]);
        var again = await RunAsync(configuration, extra);
        var refused = await RunAsync(configuration, ["--to", "0", .. extra]);   // core.setting would be dropped (E6.2)
        var down = await RunAsync(configuration, ["--to", "0", "--confirm-data-loss", .. extra]);
        var statusAfterDown = await RunAsync(configuration, ["--status", .. extra]);
        var backUp = await RunAsync(configuration, ["--to", "Initial", .. extra]);
        var latest = await RunAsync(configuration, extra);
        var finalStatus = await RunAsync(configuration, ["--status", .. extra]);

        Assert.Equal(MigrateProgram.ExitOk, up.Code);
        Assert.Contains("Direction: Up", up.Output, StringComparison.Ordinal);
        Assert.Contains("_Initial", up.Output, StringComparison.Ordinal);
        Assert.Contains("Schema is up to date.", status.Output, StringComparison.Ordinal);
        Assert.Contains("Pending (0)", status.Output, StringComparison.Ordinal);
        Assert.Contains("Direction: None", again.Output, StringComparison.Ordinal);
        Assert.Contains("Nothing to do.", again.Output, StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitConfirmationRequired, refused.Code);
        Assert.Contains("drop table core.setting", refused.Error, StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitOk, down.Code);
        Assert.Contains("Direction: Down", down.Output, StringComparison.Ordinal);
        Assert.Matches(@"Reverted \([1-9]\d*\)", down.Output);
        Assert.Contains("Applied (0)", statusAfterDown.Output, StringComparison.Ordinal);
        Assert.Contains("Schema is not up to date.", statusAfterDown.Output, StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitOk, backUp.Code);
        Assert.Contains("Direction: Up", backUp.Output, StringComparison.Ordinal);
        Assert.Contains("_Initial", backUp.Output, StringComparison.Ordinal);
        Assert.Equal(MigrateProgram.ExitOk, latest.Code);
        Assert.Contains("Schema is up to date.", finalStatus.Output, StringComparison.Ordinal);

        await AssertHistoryTableInWmsSchemaAsync(provider, connectionString);
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
