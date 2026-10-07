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
using Wolfgang.Wms.Core.Identity.BreakGlass;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.IntegrationTests.Api;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E9.3 against real engines through the API: local sign-in is open on a fresh install; the first external
/// sign-in (simulated through the gate the OIDC events call) closes it with <c>403 auth.local_login_closed</c>;
/// a timed window opened on behalf of a host user re-opens it and shows in the anonymous status; closing the
/// window early shuts it again; a second host with <c>Wms:Auth:ForceLocal</c> ignores the gate; every change
/// is audited with the actor.
/// </summary>
public sealed class LocalLoginGateTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;



    [SqlServerFact]
    public async Task SqlServer_closes_local_sign_in_after_SSO_and_reopens_it_for_a_window()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();

        await AssertGateAsync("SqlServer", database.ConnectionString, trustServerCertificate: true);
    }



    [DockerFact]
    public async Task PostgreSql_closes_local_sign_in_after_SSO_and_reopens_it_for_a_window()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertGateAsync("PostgreSql", container.GetConnectionString(), trustServerCertificate: false);
    }



    private static async Task AssertGateAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        await using (var app = await StartHostAsync(provider, connectionString, trustServerCertificate, forceLocal: false))
        {
            using var client = app.GetTestClient();
            var open = await StatusAsync(client);
            Assert.NotEmpty(await TestSessions.SignInAsAdministratorAsync(client));   // open: the bootstrap administrator signs in and changes the password
            Assert.True(open.LocalLoginOpen);
            Assert.False(open.SsoVerified);
            Assert.False(open.ForcedLocal);

            var verified = await WithGateAsync(app.Services, gate => gate.MarkSsoVerifiedAsync("oidc", CancellationToken.None));
            var again = await WithGateAsync(app.Services, gate => gate.MarkSsoVerifiedAsync("other", CancellationToken.None));   // only the first success counts
            var closed = await StatusAsync(client);
            using var refused = await LoginAsync(client);
            Assert.Equal("oidc", verified.SsoVerifiedProvider);
            Assert.Equal("oidc", again.SsoVerifiedProvider);
            AssertClose(verified.SsoVerifiedAt, again.SsoVerifiedAt);   // the second read comes back at the column's millisecond precision
            Assert.False(closed.LocalLoginOpen);
            Assert.True(closed.SsoVerified);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.Equal("auth.local_login_closed", await CodeAsync(refused));

            var window = await WithGateAsync(app.Services, gate => gate.UnlockAsync(TimeSpan.FromMinutes(5), "HOST\\ops", CancellationToken.None));
            var unlocked = await StatusAsync(client);
            using var admitted = await LoginAsync(client);
            Assert.Equal("HOST\\ops", window.UnlockedBy);
            Assert.True(unlocked.LocalLoginOpen);
            AssertClose(window.UnlockedUntil, unlocked.UnlockedUntil);
            Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);

            var locked = await WithGateAsync(app.Services, gate => gate.LockAsync("HOST\\ops", CancellationToken.None));
            var lockedAgain = await WithGateAsync(app.Services, gate => gate.LockAsync("HOST\\ops", CancellationToken.None));   // a no-op when nothing is open
            var shut = await StatusAsync(client);
            using var refusedAgain = await LoginAsync(client);
            Assert.False(locked.IsUnlockedAt(DateTimeOffset.UtcNow));
            Assert.Equal(locked.UnlockedBy, lockedAgain.UnlockedBy);
            AssertClose(locked.UnlockedUntil, lockedAgain.UnlockedUntil);
            Assert.False(shut.LocalLoginOpen);
            Assert.Null(shut.UnlockedUntil);
            Assert.Equal(HttpStatusCode.Forbidden, refusedAgain.StatusCode);

            await AssertAuditAsync(app.Services);
            await AssertRulesAsync(app.Services);
        }

        await using var forced = await StartHostAsync(provider, connectionString, trustServerCertificate, forceLocal: true);
        using var forcedClient = forced.GetTestClient();
        var overridden = await StatusAsync(forcedClient);
        using var admittedByOverride = await LoginAsync(forcedClient);
        Assert.True(overridden.LocalLoginOpen);
        Assert.True(overridden.SsoVerified);
        Assert.True(overridden.ForcedLocal);
        Assert.Equal(HttpStatusCode.OK, admittedByOverride.StatusCode);
    }



    private static async Task AssertAuditAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var headers = await context.Set<AuditHeader>().Include(h => h.Details).Where(h => h.EntityTable.Contains("local_login_gate")).ToListAsync();
        var row = await context.LocalLoginGates.SingleAsync();

        Assert.True(headers.Count >= 3, $"expected the verification, the unlock and the lock to be audited; found {headers.Count}");
        Assert.Contains(headers.SelectMany(h => h.Details), d => string.Equals(d.ColumnName, "unlocked_by", StringComparison.Ordinal) && string.Equals(d.ValueText, "HOST\\ops", StringComparison.Ordinal));
        Assert.Contains(headers.SelectMany(h => h.Details), d => string.Equals(d.ColumnName, "locked_by", StringComparison.Ordinal) && string.Equals(d.ValueText, "HOST\\ops", StringComparison.Ordinal));
        Assert.NotNull(row.LockedAt);
        Assert.True(row.RowVersion > 0);
    }



    private static async Task AssertRulesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<ILocalLoginGate>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => gate.UnlockAsync(TimeSpan.FromSeconds(10), "HOST\\ops", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => gate.UnlockAsync(TimeSpan.FromDays(31), "HOST\\ops", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => gate.UnlockAsync(TimeSpan.FromMinutes(5), " ", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => gate.LockAsync(" ", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => gate.MarkSsoVerifiedAsync(" ", CancellationToken.None));
    }



    /// <summary>
    /// Equal to the millisecond: the stored columns are <c>datetime2(3)</c> / <c>timestamptz</c>, so a value
    /// written from memory and read back can differ below that.
    /// </summary>
    private static void AssertClose(DateTimeOffset? expected, DateTimeOffset? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.True((expected.Value - actual.Value).Duration() < TimeSpan.FromMilliseconds(1), $"{expected:O} vs {actual:O}");
    }



    private static async Task<LocalLoginGateInfo> WithGateAsync(IServiceProvider services, Func<ILocalLoginGate, Task<LocalLoginGateInfo>> action)
    {
        using var scope = services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ILocalLoginGate>());
    }



    private static async Task<LocalLoginStatus> StatusAsync(HttpClient client)
    {
        return (await client.GetFromJsonAsync<LocalLoginStatus>("/api/v0/auth/local/status", Json))!;
    }



    private static Task<HttpResponseMessage> LoginAsync(HttpClient client)
    {
        var body = JsonSerializer.Serialize(new LocalLoginRequest("admin", TestSessions.AdministratorPassword), Json);
        return client.PostAsync(new Uri("/api/v0/auth/local/login", UriKind.Relative), new StringContent(body, Encoding.UTF8, "application/json"));
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }



    private static async Task<WebApplication> StartHostAsync(string provider, string connectionString, bool trustServerCertificate, bool forceLocal)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Wms:Database:Provider"] = provider,
            ["Wms:Database:ConnectionString"] = connectionString,
            ["Wms:Database:TrustServerCertificate"] = trustServerCertificate ? "true" : "false",
            ["Wms:Database:AutoMigrate"] = "true",
            ["Wms:Auth:ForceLocal"] = forceLocal ? "true" : "false",
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
}
