// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wolfgang.AuditTrail.Entities;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E9 against PostgreSQL through the API: the bootstrap administrator is created once, signs in with the
/// documented default, is held to the password change, cannot reuse the default, changes it and is free;
/// wrong passwords lock the account; the audit trail records the user without the hash; a second start
/// creates nothing.
/// </summary>
public sealed class LocalAccountsTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;



    [DockerFact]
    public async Task Bootstrap_admin_signs_in_changes_the_password_and_locks_out_after_failures()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await using (var first = await StartHostAsync(container.GetConnectionString()))
        {
            using var client = first.GetTestClient();
            var cookie = await AssertFirstSignInAndGateAsync(client);
            await AssertPasswordChangeAsync(client, cookie);
            await AssertLockoutAsync(client);
            await AssertAuditAsync(first.Services);
        }

        await using var second = await StartHostAsync(container.GetConnectionString());
        using var scope = second.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<WmsDbContext>().Users.CountAsync());   // bootstrap ignored thereafter
    }



    private static async Task<string> AssertFirstSignInAndGateAsync(HttpClient client)
    {
        using var wrong = await LoginAsync(client, "admin", "nope");
        using var login = await LoginAsync(client, "ADMIN", PasswordPolicy.BootstrapDefault);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal("auth.invalid_credentials", await CodeAsync(wrong));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);   // SignIn writes the cookie with 200
        var cookie = Cookie(login);

        var me = await MeAsync(client, cookie);
        using var gated = await client.SendAsync(Request(HttpMethod.Get, "/api/v0/settings/registry", cookie, body: null));
        using var withoutCookie = await client.GetAsync(new Uri("/api/v0/settings/registry", UriKind.Relative));
        Assert.Equal("admin", me.UserName);
        Assert.True(me.MustChangePassword);
        Assert.True(me.IsLocalAdmin);
        Assert.Equal(HttpStatusCode.Forbidden, gated.StatusCode);
        Assert.Equal("auth.password_change_required", await CodeAsync(gated));
        Assert.Equal(HttpStatusCode.Unauthorized, withoutCookie.StatusCode);   // E10.1: settings.read is required
        Assert.Equal(["*@organization"], me.Permissions);   // the local administrator holds everything
        return cookie;
    }



    private static async Task AssertPasswordChangeAsync(HttpClient client, string cookie)
    {
        using var reuse = await ChangeAsync(client, cookie, PasswordPolicy.BootstrapDefault, PasswordPolicy.BootstrapDefault);
        using var tooShort = await ChangeAsync(client, cookie, PasswordPolicy.BootstrapDefault, "short");
        using var wrongCurrent = await ChangeAsync(client, cookie, "nope", "a-long-enough-password");
        using var changed = await ChangeAsync(client, cookie, PasswordPolicy.BootstrapDefault, "a-long-enough-password");
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        Assert.Contains("documented default", await DetailOrTitleAsync(reuse), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrent.StatusCode);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var refreshed = Cookie(changed);
        var me = await MeAsync(client, refreshed);
        using var free = await client.SendAsync(Request(HttpMethod.Get, "/api/v0/settings/registry", refreshed, body: null));
        using var oldPassword = await LoginAsync(client, "admin", PasswordPolicy.BootstrapDefault);
        using var newPassword = await LoginAsync(client, "admin", "a-long-enough-password");
        using var logout = await client.SendAsync(Request(HttpMethod.Post, "/api/v0/auth/logout", refreshed, body: null));
        Assert.False(me.MustChangePassword);
        Assert.Equal(HttpStatusCode.OK, free.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), c => c.StartsWith("wms.session=", StringComparison.Ordinal) && c.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }



    private static async Task AssertLockoutAsync(HttpClient client)
    {
        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            last?.Dispose();
            last = await LoginAsync(client, "admin", "nope");
        }

        using var lockedResponse = last!;
        using var evenTheRightPassword = await LoginAsync(client, "admin", "a-long-enough-password");
        Assert.Equal(HttpStatusCode.Locked, lockedResponse.StatusCode);
        Assert.Equal("auth.locked_out", await CodeAsync(lockedResponse));
        Assert.Equal(HttpStatusCode.Locked, evenTheRightPassword.StatusCode);
    }



    private static async Task AssertAuditAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var headers = await context.Set<AuditHeader>().Include(h => h.Details).Where(h => h.EntityTable.Contains("user")).ToListAsync();
        var user = await context.Users.SingleAsync();

        Assert.NotEmpty(headers);
        Assert.DoesNotContain(headers.SelectMany(h => h.Details), d => string.Equals(d.ColumnName, "password_hash", StringComparison.Ordinal));
        Assert.Contains(headers.SelectMany(h => h.Details), d => string.Equals(d.ColumnName, "must_change_password", StringComparison.Ordinal));
        Assert.StartsWith("AQAAAA", user.PasswordHash, StringComparison.Ordinal);   // Identity v3 PBKDF2 format
        Assert.NotNull(user.LockedUntil);
        Assert.NotNull(user.SessionsValidAfter);
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
        builder.Services.AddWmsModules();
        builder.Services.AddWmsSettingsModule();
        builder.Services.AddWmsAuthModule();
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        var app = builder.Build();
        app.UseWmsProblemDetails();
        app.UseWmsAuth();
        app.MapWmsApi().MapWmsModules();
        await app.StartAsync();
        return app;
    }



    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string userName, string password)
    {
        return client.SendAsync(Request(HttpMethod.Post, "/api/v0/auth/local/login", cookie: null, JsonSerializer.Serialize(new LocalLoginRequest(userName, password), Json)));
    }



    private static Task<HttpResponseMessage> ChangeAsync(HttpClient client, string cookie, string current, string replacement)
    {
        return client.SendAsync(Request(HttpMethod.Post, "/api/v0/auth/local/password", cookie, JsonSerializer.Serialize(new ChangePasswordRequest(current, replacement), Json)));
    }



    private static async Task<SessionInfo> MeAsync(HttpClient client, string cookie)
    {
        using var response = await client.SendAsync(Request(HttpMethod.Get, "/api/v0/auth/me", cookie, body: null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionInfo>(Json))!;
    }



    private static HttpRequestMessage Request(HttpMethod method, string path, string? cookie, string? body)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return request;
    }



    private static string Cookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("wms.session=", StringComparison.Ordinal));
        return setCookie.Split(';')[0];
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }



    private static async Task<string> DetailOrTitleAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (problem.RootElement.TryGetProperty("detail", out var detail) ? detail.GetString() : null) ?? problem.RootElement.GetProperty("title").GetString() ?? string.Empty;
    }
}
