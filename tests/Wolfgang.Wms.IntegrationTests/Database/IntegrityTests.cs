// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Identity;
using Wolfgang.Wms.Infrastructure.Integrity;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.IntegrationTests.Api;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E10.4 against PostgreSQL: every user, role and assignment is signed on save with a key stored protected
/// in <c>wms.integrity_key</c>; an assignment, a role or a user changed through the database grants
/// nothing (403 on sign-in for the user); the verification job counts the failures; a database without
/// the key (the first start after the upgrade) gets its key and its rows signed once.
/// </summary>
public sealed class IntegrityTests
{
    private const string Password = "a-long-enough-password";
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;



    [DockerFact]
    public async Task Rows_are_signed_verified_on_read_and_counted_by_the_job()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await using (var first = await StartHostAsync(container.GetConnectionString()))
        {
            using var client = first.GetTestClient();
            var admin = await TestSessions.SignInAsAdministratorAsync(client);
            client.DefaultRequestHeaders.Add("Cookie", admin);
            await AssertEverythingSignedAsync(first.Services);
            var bob = await CreateUserAsync(first.Services, "bob");
            var roles = (await client.GetFromJsonAsync<List<RoleInfo>>("/api/v0/auth/roles", Json))!;
            await AssignAsync(client, admin, bob, roles.Single(r => string.Equals(r.Name, "Administrator", StringComparison.Ordinal)).Id);
            Assert.Equal(HttpStatusCode.OK, await RolesStatusAsync(first, await SignInAsync(client, "bob")));

            await TamperAsync(first.Services, "UPDATE core.user_role SET site_id = 7 WHERE user_id = " + bob);
            Assert.Equal(HttpStatusCode.Forbidden, await RolesStatusAsync(first, await SignInAsync(client, "bob")));   // the assignment grants nothing
            Assert.Equal(1, (await first.Services.GetRequiredService<IntegrityVerificationJob>().RunOnceAsync(CancellationToken.None)).Failed);

            var custom = await CreateRoleAsync(client, admin);
            await AssignAsync(client, admin, bob, custom);
            Assert.Equal(HttpStatusCode.OK, await RolesStatusAsync(first, await SignInAsync(client, "bob")));
            await TamperAsync(first.Services, "INSERT INTO core.role_permission (role_id, permission_name) VALUES (" + custom + ", 'auth.roles.write')");
            Assert.Equal(HttpStatusCode.Forbidden, await RolesStatusAsync(first, await SignInAsync(client, "bob")));   // the role grants nothing

            await TamperAsync(first.Services, "UPDATE core.\"user\" SET is_local_admin = true WHERE id = " + bob);
            using var login = await client.PostAsync(new Uri("/api/v0/auth/local/login", UriKind.Relative), Body(new LocalLoginRequest("bob", Password)));
            Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
            Assert.Equal("auth.integrity_failure", (await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync())).RootElement.GetProperty("code").GetString());
            var result = await first.Services.GetRequiredService<IntegrityVerificationJob>().RunOnceAsync(CancellationToken.None);
            Assert.Equal(3, result.Failed);
            Assert.Same(result, first.Services.GetRequiredService<IntegrityVerificationJob>().LastResult);

            await TamperAsync(first.Services, "UPDATE core.\"user\" SET is_local_admin = false WHERE id = " + bob);
            await TamperAsync(first.Services, "DELETE FROM wms.integrity_key");
        }

        await using var second = await StartHostAsync(container.GetConnectionString());   // the upgrade start: a new key, every row signed once
        using var later = second.GetTestClient();
        await AssertEverythingSignedAsync(second.Services);
        Assert.Equal(0, (await second.Services.GetRequiredService<IntegrityVerificationJob>().RunOnceAsync(CancellationToken.None)).Failed);
        Assert.Equal(HttpStatusCode.OK, await RolesStatusAsync(second, await SignInAsync(later, "bob")));   // the documented limitation: rows are signed as they stand, so the permission added through the database now grants
    }



    private static async Task AssertEverythingSignedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var keys = await context.Set<IntegrityKey>().ToListAsync();
        var signer = scope.ServiceProvider.GetRequiredService<IIntegritySigner>();

        Assert.Single(keys);
        Assert.StartsWith("enc:v1:", keys[0].ProtectedKey, StringComparison.Ordinal);
        Assert.DoesNotContain(await context.Users.ToListAsync(), u => u.Signature is null);
        Assert.DoesNotContain(await context.Roles.ToListAsync(), r => r.Signature is null);
        Assert.DoesNotContain(await context.UserRoles.ToListAsync(), a => a.Signature is null);
        Assert.True(await signer.IsValidAsync(await context.Roles.Include(r => r.Permissions).FirstAsync(r => r.BuiltInKey != null), "core.role", CancellationToken.None));

        context.Users.Add(new User { UserName = "sync", UserNameNormalized = "SYNC", DisplayName = "sync" });
        var refused = Record.Exception(() => context.SaveChanges());
        Assert.Contains("SaveChangesAsync", refused?.ToString(), StringComparison.Ordinal);   // the synchronous save never writes a signed row
    }



    private static async Task<long> CreateUserAsync(IServiceProvider services, string userName)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var user = new User { UserName = userName, UserNameNormalized = EfLocalAccounts.Normalize(userName), DisplayName = userName, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        user.PasswordHash = hasher.HashPassword(user, Password);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        Assert.NotNull(user.Signature);   // signed by the interceptor
        return user.Id;
    }



    private static async Task<long> CreateRoleAsync(HttpClient client, string admin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v0/auth/roles", UriKind.Relative)) { Content = Body(new RoleDraft("Readers", "reads roles", ["auth.roles.read"])) };
        request.Headers.Add("Cookie", admin);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RoleInfo>(Json))!.Id;
    }



    private static async Task AssignAsync(HttpClient client, string admin, long userId, long roleId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v0/auth/users/{userId}/roles", UriKind.Relative)) { Content = Body(new AssignRoleRequest(roleId)) };
        request.Headers.Add("Cookie", admin);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }



    private static async Task<string> SignInAsync(HttpClient client, string userName)
    {
        using var login = await client.PostAsync(new Uri("/api/v0/auth/local/login", UriKind.Relative), Body(new LocalLoginRequest(userName, Password)));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return login.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
    }



    private static async Task<HttpStatusCode> RolesStatusAsync(WebApplication app, string cookie)
    {
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v0/auth/roles", UriKind.Relative));
        request.Headers.Add("Cookie", cookie);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }



    private static async Task TamperAsync(IServiceProvider services, string sql)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
#pragma warning disable EF1002 // the statements are test constants with numeric ids
        await context.Database.ExecuteSqlRawAsync(sql);
#pragma warning restore EF1002
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
        builder.Services.AddWmsRolesModule();
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        builder.Services.AddWmsIntegrityVerification();
        var app = builder.Build();
        app.UseWmsProblemDetails();
        app.UseWmsAuth();
        app.MapWmsApi().MapWmsModules();
        await app.StartAsync();
        return app;
    }



    private static StringContent Body<T>(T value)
    {
        return new StringContent(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");
    }
}
