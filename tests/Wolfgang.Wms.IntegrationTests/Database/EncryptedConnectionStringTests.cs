// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Schema;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.Migrate;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E8.1/E8.2 end to end on PostgreSQL: the migrate tool encrypts the connection string with a file key ring,
/// the host starts from the <c>enc:v1:</c> value and the same ring, migrates and serves; the tool migrates
/// with it too; a host pointed at an empty ring refuses to start with the clear message.
/// </summary>
public sealed class EncryptedConnectionStringTests
{
    [DockerFact]
    public async Task Host_and_tool_start_from_an_encrypted_connection_string_with_the_shared_file_ring()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();
        var ring = Path.Combine(Path.GetTempPath(), "wms-ring-" + Guid.NewGuid().ToString("N"));
        var emptyRing = Path.Combine(Path.GetTempPath(), "wms-ring-" + Guid.NewGuid().ToString("N"));
        try
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var protectExit = await MigrateProgram.RunAsync(["--protect", "--provider", "PostgreSql", "--connection-string", container.GetConnectionString(), "--key-ring", ring], output, error, configuration: null, CancellationToken.None);
            var encrypted = output.ToString().Trim();
            Assert.Equal(MigrateProgram.ExitOk, protectExit);
            Assert.StartsWith("enc:v1:", encrypted, StringComparison.Ordinal);

            var toolExit = await MigrateProgram.RunAsync(["--status", "--provider", "PostgreSql", "--connection-string", encrypted, "--key-ring", ring], output, error, configuration: null, CancellationToken.None);
            Assert.Equal(MigrateProgram.ExitOk, toolExit);
            Assert.Contains("Reachable: yes", output.ToString(), StringComparison.Ordinal);

            await using var app = await StartHostAsync(encrypted, ring);
            using var client = app.GetTestClient();
            using var response = await client.GetAsync(new Uri("/api/v0/system/schema", UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"upToDate\":true", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            var refused = await Assert.ThrowsAsync<InvalidOperationException>(async () => await StartHostAsync(encrypted, emptyRing));
            Assert.Contains("does not hold the key", refused.Message, StringComparison.Ordinal);
            var noRingExit = await MigrateProgram.RunAsync(["--status", "--provider", "PostgreSql", "--connection-string", encrypted], output, error, configuration: null, CancellationToken.None);
            Assert.Equal(MigrateProgram.ExitUsage, noRingExit);
            Assert.Contains("Wms:DataProtection:KeyRingPath", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            foreach (var path in new[] { ring, emptyRing }.Where(Directory.Exists))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }



    private static async Task<WebApplication> StartHostAsync(string connectionString, string ring)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Wms:Database:Provider"] = "PostgreSql",
            ["Wms:Database:ConnectionString"] = connectionString,
            ["Wms:Database:AutoMigrate"] = "true",
            [KeyRingOptions.PathKey] = ring,
        });
        builder.Services.AddWmsApiVersioning();
        builder.Services.AddWmsModules();
        builder.Services.AddWmsSchemaModule();
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        var app = builder.Build();
        app.MapWmsApi().MapWmsModules();
        try
        {
            await app.StartAsync();
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        return app;
    }
}
