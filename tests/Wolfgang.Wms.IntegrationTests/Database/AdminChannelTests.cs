// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wolfgang.AuditTrail.Entities;
using Wolfgang.Wms.Admin;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Identity.BreakGlass;
using Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E9.3 end to end on a real engine, a real pipe and the real tool: the host listens on its channel; after
/// single sign-on is verified <c>wms-admin unlock</c> reopens local sign-in for a window, <c>lock</c> closes
/// it, <c>status</c> reports it; a tool with another key ring is refused; the audit row names the user who
/// ran the tool.
/// </summary>
public sealed class AdminChannelTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private static readonly TimeSpan ListenTimeout = TimeSpan.FromSeconds(20);



    [SqlServerFact]
    public async Task SqlServer_unlock_lock_and_status_through_the_tool()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();

        await AssertChannelAsync("SqlServer", database.ConnectionString, trustServerCertificate: true);
    }



    [DockerFact]
    public async Task PostgreSql_unlock_lock_and_status_through_the_tool()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertChannelAsync("PostgreSql", container.GetConnectionString(), trustServerCertificate: false);
    }



    private static async Task AssertChannelAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        await TestMigrations.ApplyAsync(provider, connectionString);
        var runtime = await TestLogins.CreateRuntimeAsync(provider, connectionString);

        var ring = Directory.CreateTempSubdirectory("wms-admin-ring-");
        var otherRing = Directory.CreateTempSubdirectory("wms-admin-other-ring-");
        var channel = "wms-admin-test-" + Guid.NewGuid().ToString("N");
        try
        {
            await using var app = await StartHostAsync(provider, runtime, trustServerCertificate, ring.FullName, channel);
            using var client = app.GetTestClient();
            var tool = new Tool(ring.FullName, channel);

            var listening = await tool.WaitUntilListeningAsync();
            Assert.Equal(AdminProgram.ExitOk, listening.Exit);
            Assert.Contains("Local sign-in is open.", listening.Output, StringComparison.Ordinal);

            using (var scope = app.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<ILocalLoginGate>().MarkSsoVerifiedAsync("oidc", CancellationToken.None);
            }

            var closed = await tool.RunAsync("status");
            var unlocked = await tool.RunAsync("unlock", "--minutes", "5");
            var open = await StatusAsync(client);
            var foreign = await new Tool(otherRing.FullName, channel).RunAsync("status");
            var locked = await tool.RunAsync("lock");
            var shut = await StatusAsync(client);

            Assert.Equal(AdminProgram.ExitOk, closed.Exit);
            Assert.Contains("closed (single sign-on verified", closed.Output, StringComparison.Ordinal);
            Assert.Equal(AdminProgram.ExitOk, unlocked.Exit);
            Assert.Contains("unlocked until", unlocked.Output, StringComparison.Ordinal);
            Assert.True(open.LocalLoginOpen);
            Assert.NotNull(open.UnlockedUntil);
            Assert.Equal(AdminProgram.ExitRefused, foreign.Exit);
            Assert.Contains("not sealed with this host's key ring", foreign.Error, StringComparison.Ordinal);
            Assert.Equal(AdminProgram.ExitOk, locked.Exit);
            Assert.False(shut.LocalLoginOpen);

            await AssertAuditAsync(app.Services);
        }
        finally
        {
            ring.Delete(recursive: true);
            otherRing.Delete(recursive: true);
        }
    }



    private static async Task AssertAuditAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var row = await context.LocalLoginGates.SingleAsync();
        var details = await context.Set<AuditHeader>().Include(h => h.Details).Where(h => h.EntityTable.Contains("local_login_gate")).SelectMany(h => h.Details).ToListAsync();

        // A Windows pipe reports the connected account (user name only); elsewhere the tool's own claim is recorded.
        Assert.False(string.IsNullOrWhiteSpace(row.UnlockedBy));
        Assert.EndsWith(Environment.UserName, row.UnlockedBy, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(row.UnlockedBy, row.LockedBy);
        Assert.Contains(details, d => string.Equals(d.ColumnName, "unlocked_by", StringComparison.Ordinal) && string.Equals(d.ValueText, row.UnlockedBy, StringComparison.Ordinal));
    }



    private static async Task<LocalLoginStatus> StatusAsync(HttpClient client)
    {
        return (await client.GetFromJsonAsync<LocalLoginStatus>("/api/v0/auth/local/status", Json))!;
    }



    private static async Task<WebApplication> StartHostAsync(string provider, string connectionString, bool trustServerCertificate, string keyRing, string channel)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Wms:Database:Provider"] = provider,
            ["Wms:Database:ConnectionString"] = connectionString,
            ["Wms:Database:TrustServerCertificate"] = trustServerCertificate ? "true" : "false",
            [KeyRingOptions.PathKey] = keyRing,
            [AdminChannelOptions.NameKey] = channel,
        });
        builder.Services.AddWmsApiVersioning();
        builder.Services.AddWmsProblemDetails();
        builder.Services.AddWmsModules();
        builder.Services.AddWmsSettingsModule();
        builder.Services.AddWmsAuthModule();
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        builder.Services.AddWmsAdminChannel(builder.Configuration);
        var app = builder.Build();
        app.UseWmsProblemDetails();
        app.UseWmsAuth();
        app.MapWmsApi().MapWmsModules();
        await app.StartAsync();
        return app;
    }



    /// <summary>
    /// The tool, driven in-process with its streams captured.
    /// </summary>
    private sealed class Tool(string keyRing, string channel)
    {
        public async Task<(int Exit, string Output, string Error)> RunAsync(params string[] arguments)
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var exit = await AdminProgram.RunAsync([.. arguments, "--key-ring", keyRing, "--channel", channel], output, error, new ConfigurationBuilder().Build(), TimeProvider.System, CancellationToken.None);
            return (exit, output.ToString(), error.ToString());
        }



        /// <summary>
        /// The host's server starts after the application has started; retry status until it answers.
        /// </summary>
        public async Task<(int Exit, string Output, string Error)> WaitUntilListeningAsync()
        {
            var deadline = TimeProvider.System.GetUtcNow() + ListenTimeout;
            (int Exit, string Output, string Error) last;
            do
            {
                last = await RunAsync("status");
                if (last.Exit != AdminProgram.ExitUnreachable)
                {
                    return last;
                }

                await Task.Delay(250);
            }
            while (TimeProvider.System.GetUtcNow() < deadline);

            return last;
        }
    }
}
