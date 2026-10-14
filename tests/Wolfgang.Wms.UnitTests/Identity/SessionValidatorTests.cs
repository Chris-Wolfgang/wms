// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using System.Security.Claims;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.UnitTests.Identity;

/// <summary>
/// E10.5 without a host: a session is rejected without an issue time, past the absolute lifetime, or when
/// issued before the user's "sessions valid after" (or for a user who no longer exists); otherwise it lives;
/// the idle-timeout setting validates its range; the placeholder revocations revoke nothing.
/// </summary>
public sealed class SessionValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly ISettings Settings = new DefaultSettings(new SettingRegistry([AuthSettings.SessionLifetime, AuthSettings.IdleTimeout]));



    [Fact]
    public async Task Sessions_are_rejected_without_an_issue_time_past_the_lifetime_or_after_revocation()
    {
        var revocations = new FakeRevocations { ValidAfter = Now.AddHours(-1) };
        await revocations.RevokeSessionsAsync(42, CancellationToken.None);   // the fake ignores it; keeps the fake fully exercised

        Assert.Contains("no issue time", (await SessionValidator.ReasonToRejectAsync(Principal(42, signedInAt: null), Now, Settings, revocations, CancellationToken.None))!, StringComparison.Ordinal);
        Assert.Contains("absolute lifetime", (await SessionValidator.ReasonToRejectAsync(Principal(42, Now.AddHours(-9)), Now, Settings, revocations, CancellationToken.None))!, StringComparison.Ordinal);
        Assert.Contains("revoked", (await SessionValidator.ReasonToRejectAsync(Principal(42, Now.AddHours(-2)), Now, Settings, revocations, CancellationToken.None))!, StringComparison.Ordinal);
        Assert.Null(await SessionValidator.ReasonToRejectAsync(Principal(42, Now.AddMinutes(-30)), Now, Settings, revocations, CancellationToken.None));
        Assert.Contains("names no user", (await SessionValidator.ReasonToRejectAsync(Principal(null, Now.AddMinutes(-30)), Now, Settings, revocations, CancellationToken.None))!, StringComparison.Ordinal);
        Assert.Null(await SessionValidator.ReasonToRejectAsync(Principal(42, Now.AddMinutes(-30)), Now, Settings, new NoSessionRevocations(), CancellationToken.None));
        Assert.Contains("revoked", (await SessionValidator.ReasonToRejectAsync(Principal(42, Now.AddMinutes(-30)), Now, Settings, new FakeRevocations { ValidAfter = DateTimeOffset.MaxValue }, CancellationToken.None))!, StringComparison.Ordinal);   // a disabled or deleted user
        await Assert.ThrowsAsync<ArgumentNullException>(() => SessionValidator.ReasonToRejectAsync(Principal(42, Now), Now, null!, revocations, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => SessionValidator.ReasonToRejectAsync(Principal(42, Now), Now, Settings, null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => SessionValidator.OnSigningInAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => SessionValidator.OnValidatePrincipalAsync(null!));
    }



    [Fact]
    public async Task Idle_timeout_setting_validates_its_range_and_the_placeholder_revokes_nothing()
    {
        var revocations = new NoSessionRevocations();

        await revocations.RevokeSessionsAsync(1, CancellationToken.None);

        Assert.Null(await revocations.SessionsValidAfterAsync(1, CancellationToken.None));
        Assert.Null(AuthSettings.IdleTimeout.Validate(TimeSpan.FromMinutes(30)));
        Assert.NotNull(AuthSettings.IdleTimeout.Validate(TimeSpan.FromSeconds(30)));
        Assert.Contains(AuthSettings.IdleTimeout, AuthSettings.All);
    }



    [Fact]
    public async Task WithGrants_replaces_only_the_permission_claims_and_the_placeholder_keeps_the_session_s()
    {
        var identity = new ClaimsIdentity(SessionClaims.LocalAuthenticationType, SessionClaims.UserName, roleType: null);
        identity.AddClaim(new Claim(SessionClaims.UserId, "42"));
        identity.AddClaim(new Claim(SessionClaims.UserName, "bob"));
        identity.AddClaim(new Claim(PermissionClaims.ClaimType, "old"));

        var replaced = SessionClaims.WithGrants(new ClaimsPrincipal(identity), ["new-a", "new-b"]);

        Assert.Equal(["new-a", "new-b"], replaced.FindAll(PermissionClaims.ClaimType).Select(c => c.Value));
        Assert.Equal(42, SessionClaims.UserIdOf(replaced));
        Assert.Equal("bob", replaced.Identity!.Name);
        Assert.Equal(SessionClaims.LocalAuthenticationType, replaced.Identity.AuthenticationType);
        Assert.Throws<ArgumentNullException>(() => SessionClaims.WithGrants(null!, []));
        Assert.Throws<ArgumentNullException>(() => SessionClaims.WithGrants(replaced, null!));
        Assert.Null(await new NoSessionGrants().GrantsOfAsync(42, CancellationToken.None));
    }



    private static ClaimsPrincipal Principal(long? userId, DateTimeOffset? signedInAt)
    {
        var identity = new ClaimsIdentity("test");
        if (userId is { } id)
        {
            identity.AddClaim(new Claim(SessionClaims.UserId, id.ToString(CultureInfo.InvariantCulture)));
        }

        if (signedInAt is { } at)
        {
            identity.AddClaim(new Claim(SessionValidator.SignedInAtClaim, at.ToString("O", CultureInfo.InvariantCulture)));
        }

        return new ClaimsPrincipal(identity);
    }



    private sealed class FakeRevocations : ISessionRevocations
    {
        public DateTimeOffset? ValidAfter { get; init; }

        public Task<DateTimeOffset?> SessionsValidAfterAsync(long userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(ValidAfter);
        }

        public Task RevokeSessionsAsync(long userId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
