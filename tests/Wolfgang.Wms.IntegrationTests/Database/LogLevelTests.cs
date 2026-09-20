// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Logging;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.IntegrationTests.Api;
using Wolfgang.Wms.Logging;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E12.4 against PostgreSQL: an administrator elevates to Debug for ten minutes and the host's switch moves
/// at once (the settings hold the elevation for every instance); ending it reverts the switch; the audit
/// of the change rides the settings store.
/// </summary>
public sealed class LogLevelTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;



    [DockerFact]
    public async Task A_timed_elevation_moves_the_switch_and_reverts_on_demand()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();
        await using var app = await StartHostAsync(container.GetConnectionString());
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", await TestSessions.SignInAsAdministratorAsync(client));
        var level = app.Services.GetRequiredService<WmsLogLevel>();

        using var elevated = await client.PostAsync(new Uri("/api/v0/system/logging/elevate", UriKind.Relative), Body(new ElevateLogLevelRequest("Debug", 10)));
        var running = await elevated.Content.ReadFromJsonAsync<LoggingStatus>(Json);
        var whileElevated = level.Current;
        using var ended = await client.DeleteAsync(new Uri("/api/v0/system/logging/elevate", UriKind.Relative));
        var after = await ended.Content.ReadFromJsonAsync<LoggingStatus>(Json);

        Assert.Equal(HttpStatusCode.OK, elevated.StatusCode);
        Assert.Equal((Microsoft.Extensions.Logging.LogLevel.Debug, Microsoft.Extensions.Logging.LogLevel.Debug), (running!.EffectiveLevel, running.ElevatedLevel));
        Assert.NotNull(running.ElevatedUntil);
        Assert.Equal(LogEventLevel.Debug, whileElevated);
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        Assert.Equal((Microsoft.Extensions.Logging.LogLevel.Information, (Microsoft.Extensions.Logging.LogLevel?)null), (after!.EffectiveLevel, after.ElevatedLevel));
        Assert.Equal(LogEventLevel.Information, level.Current);
    }



    private static async Task<WebApplication> StartHostAsync(string connectionString)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Wms:Database:Provider"] = "PostgreSql",
            ["Wms:Database:ConnectionString"] = connectionString,
            ["Wms:Database:AutoMigrate"] = "true",
            ["Wms:Logging:Stdout"] = "false",
        });
        builder.UseWmsSerilog();
        builder.Services.AddWmsApiVersioning();
        builder.Services.AddWmsProblemDetails();
        builder.Services.AddWmsModules();
        builder.Services.AddWmsSettingsModule();
        builder.Services.AddWmsAuthModule();
        builder.Services.AddWmsRolesModule();
        builder.Services.AddWmsLoggingModule();
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        var app = builder.Build();
        app.UseWmsProblemDetails();
        app.UseWmsAuth();
        app.UseWmsCorrelation();
        app.MapWmsApi().MapWmsModules();
        await app.StartAsync();
        return app;
    }



    private static StringContent Body<T>(T value)
    {
        return new StringContent(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");
    }
}
