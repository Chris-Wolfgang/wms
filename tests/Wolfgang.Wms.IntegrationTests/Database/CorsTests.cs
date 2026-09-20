// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.IntegrationTests.Api;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E10.6 against PostgreSQL: the CORS allow-list is a setting; listing an origin grants it (with credentials)
/// on the next request, clearing it revokes it, an invalid origin is refused, all without a restart.
/// </summary>
public sealed class CorsTests
{
    [DockerFact]
    public async Task Allowed_origins_apply_without_a_restart()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();
        await using var app = await StartHostAsync(container.GetConnectionString());
        using var client = app.GetTestClient();
        var cookie = await TestSessions.SignInAsAdministratorAsync(client);

        using var before = await PreflightAsync(client, "https://apps.example.com");
        using var invalid = await SetAsync(client, cookie, "https://apps.example.com/path", ifMatch: null);
        using var set = await SetAsync(client, cookie, "https://apps.example.com, http://localhost:5173", ifMatch: null);
        using var granted = await PreflightAsync(client, "https://apps.example.com");
        using var other = await PreflightAsync(client, "https://evil.example.com");
        var etag = set.Headers.ETag?.ToString();
        using var cleared = await SetAsync(client, cookie, string.Empty, etag);
        using var after = await PreflightAsync(client, "https://apps.example.com");

        Assert.DoesNotContain("Access-Control-Allow-Origin", before.Headers.Select(h => h.Key));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Equal("https://apps.example.com", granted.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", granted.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        Assert.DoesNotContain("Access-Control-Allow-Origin", other.Headers.Select(h => h.Key));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.DoesNotContain("Access-Control-Allow-Origin", after.Headers.Select(h => h.Key));
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
        });
        builder.Services.AddWmsApiVersioning();
        builder.Services.AddWmsProblemDetails();
        builder.Services.AddWmsCors();
        builder.Services.AddWmsModules();
        builder.Services.AddWmsSettingsModule();
        builder.Services.AddWmsAuthModule();
        builder.Services.AddWmsRolesModule();
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        var app = builder.Build();
        app.UseWmsProblemDetails();
        app.UseWmsCors();
        app.UseWmsAuth();
        app.MapWmsApi().MapWmsModules();
        await app.StartAsync();
        return app;
    }



    private static Task<HttpResponseMessage> PreflightAsync(HttpClient client, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/api/v0/auth/me", UriKind.Relative));
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return client.SendAsync(request);
    }



    private static Task<HttpResponseMessage> SetAsync(HttpClient client, string cookie, string value, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, new Uri("/api/v0/settings/organization/0/" + WmsCors.AllowedOrigins.Name, UriKind.Relative))
        {
            Content = new StringContent(JsonSerializer.Serialize(new SetSettingRequest(value), JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Cookie", cookie);
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return client.SendAsync(request);
    }
}
