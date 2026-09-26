// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.AuditTrail;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Identity;
using Wolfgang.Wms.UnitTests.Database;

namespace Wolfgang.Wms.UnitTests.Identity;

/// <summary>
/// E9 without a database: the password policy, the session claims, the placeholder accounts and the
/// exception handler, the module registration, and the user table's shape (password hash never audited).
/// </summary>
public sealed class LocalAccountsUnitTests
{
    [Theory]
    [InlineData(null, "at least 12")]
    [InlineData("short", "at least 12")]
    [InlineData("ChangeMe-2026!", "documented default")]
    [InlineData("a-long-enough-password", null)]
    public void Password_policy_requires_length_and_refuses_the_default(string? password, string? expectedFragment)
    {
        var reason = PasswordPolicy.Check(password);

        if (expectedFragment is null)
        {
            Assert.Null(reason);
        }
        else
        {
            Assert.Contains(expectedFragment, reason, StringComparison.Ordinal);
        }
    }



    [Fact]
    public void Session_claims_round_trip_the_local_user()
    {
        var user = new LocalUser(42, "alice", "Alice", MustChangePassword: true, IsLocalAdmin: true, IsDisabled: false, Grants: ["*@organization"]);

        var principal = SessionClaims.Principal(user);

        Assert.Equal(42, SessionClaims.UserIdOf(principal));
        Assert.True(SessionClaims.MustChangePasswordOf(principal));
        Assert.Equal("alice", principal.Identity?.Name);
        Assert.Equal("local", principal.Identity?.AuthenticationType);
        Assert.Equal("true", principal.FindFirst(SessionClaims.LocalAdmin)?.Value);
        Assert.Equal(["*@organization"], Wolfgang.Wms.Core.Authorization.PermissionClaims.GrantsOf(principal));
        Assert.Null(SessionClaims.UserIdOf(new ClaimsPrincipal()));
        Assert.Null(SessionClaims.UserIdOf(null));
        Assert.False(SessionClaims.MustChangePasswordOf(null));
        Assert.Throws<ArgumentNullException>(() => SessionClaims.Principal(null!));
        Assert.Throws<ArgumentNullException>(() => LocalLoginResult.Succeeded(null!));
        Assert.Equal(LocalLoginOutcome.LockedOut, LocalLoginResult.Refused(LocalLoginOutcome.LockedOut, DateTimeOffset.UnixEpoch).Outcome);
    }



    [Fact]
    public async Task Placeholder_accounts_refuse_sign_in_and_the_handler_answers_the_code()
    {
        var accounts = new NoLocalAccounts();
        var handler = new AuthExceptionHandler();
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider() };
        context.Response.Body = new MemoryStream();

        var login = await Assert.ThrowsAsync<AuthException>(() => accounts.LoginAsync("admin", "x", CancellationToken.None));
        var change = await Assert.ThrowsAsync<AuthException>(() => accounts.ChangePasswordAsync(1, "x", "y", CancellationToken.None));
        var handled = await handler.TryHandleAsync(context, login, CancellationToken.None);

        Assert.Equal(AuthErrorCodes.Unavailable, login.Code);
        Assert.Equal(AuthErrorCodes.Unavailable, change.Code);
        Assert.Null(await accounts.FindAsync(1, CancellationToken.None));
        Assert.False(await accounts.EnsureBootstrapAdminAsync("admin", CancellationToken.None));
        Assert.True(handled);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.False(await handler.TryHandleAsync(context, new InvalidOperationException(), CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => new AuthException(null!, "d"));
        Assert.Equal("cause", new AuthException(AuthErrorCodes.Forbidden, "d", new InvalidOperationException("cause")).InnerException?.Message);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await handler.TryHandleAsync(null!, login, CancellationToken.None));
    }



    [Fact]
    public void Module_registers_cookie_authentication_the_placeholder_and_its_settings()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddWmsAuthModule();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<NoLocalAccounts>(provider.GetRequiredService<ILocalAccounts>());
        Assert.Contains(provider.GetRequiredService<ModuleCollection>().Modules, m => string.Equals(m.Name, "auth", StringComparison.Ordinal));
        Assert.Equal(["api.cors.allowed_origins", "auth.integrity.verify_interval", "auth.local.lockout_duration", "auth.local.lockout_threshold", "auth.session.idle_timeout", "auth.session.lifetime"], AuthSettings.All.Select(k => k.Name).Order(StringComparer.Ordinal));
        Assert.NotNull(AuthSettings.LockoutThreshold.Validate(0));
        Assert.Null(AuthSettings.LockoutThreshold.Validate(5));
        Assert.NotNull(AuthSettings.SessionLifetime.Validate(TimeSpan.FromHours(25)));
        Assert.NotNull(AuthSettings.LockoutDuration.Validate(TimeSpan.Zero));
        Assert.Throws<ArgumentNullException>(() => AuthModule.AddWmsAuthModule(null!));
        Assert.Throws<ArgumentNullException>(() => AuthModule.UseWmsAuth(null!));
    }



    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void User_table_is_core_user_with_a_unique_normalised_name_and_an_unaudited_hash(string provider)
    {
        using var context = new WmsDbContext(ModelConventionsTests.Options<WmsDbContext>(provider));
        var entity = context.Model.FindEntityType(typeof(User))!;

        Assert.Equal(("core", "user"), (entity.GetSchema(), entity.GetTableName()));
        Assert.Contains("ux_user_user_name_normalized", entity.GetIndexes().Select(i => i.GetDatabaseName()));
        Assert.Equal("password_hash", entity.FindProperty(nameof(User.PasswordHash))!.GetColumnName());
        Assert.NotNull(typeof(User).GetProperty(nameof(User.PasswordHash))!.GetCustomAttributes(typeof(NotAuditedAttribute), inherit: false).SingleOrDefault());
        Assert.Equal(["trg_user_row_version"], entity.GetDeclaredTriggers().Select(t => t.ModelName));
        Assert.Equal("ALICE", EfLocalAccounts.Normalize(" alice "));
        Assert.Throws<ArgumentNullException>(() => EfLocalAccounts.Normalize(null!));
        Assert.Throws<ArgumentNullException>(() => new UserConfiguration().Configure(null!));
        Assert.NotNull(context.Users);
    }
}
