// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Identity.BreakGlass;

namespace Wolfgang.Wms.UnitTests.Identity;

/// <summary>
/// E9.3 without a database: the gate is open until SSO is verified, closed afterwards unless a window is
/// open or the override is set; the window limits; the anonymous status view; the placeholder gate; and the
/// error code the module declares.
/// </summary>
public sealed class LocalLoginGateUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);



    [Fact]
    public void Before_SSO_is_verified_the_gate_is_open()
    {
        var gate = LocalLoginGateInfo.Open;

        Assert.False(gate.IsSsoVerified);
        Assert.False(gate.IsUnlockedAt(Now));
        Assert.True(gate.IsOpenAt(Now, forceLocal: false));
        Assert.Equal(new LocalLoginStatus(LocalLoginOpen: true, SsoVerified: false, UnlockedUntil: null, ForcedLocal: false), gate.ToStatus(Now, forceLocal: false));
    }



    [Fact]
    public void Once_SSO_is_verified_the_gate_is_closed_unless_a_window_is_open_or_local_is_forced()
    {
        var verified = LocalLoginGateInfo.Open with { SsoVerifiedAt = Now.AddDays(-1), SsoVerifiedProvider = "oidc" };
        var unlocked = verified with { UnlockedUntil = Now.AddMinutes(30), UnlockedBy = "HOST\\ops" };
        var expired = verified with { UnlockedUntil = Now.AddMinutes(-1), UnlockedBy = "HOST\\ops" };

        Assert.True(verified.IsSsoVerified);
        Assert.False(verified.IsOpenAt(Now, forceLocal: false));
        Assert.True(verified.IsOpenAt(Now, forceLocal: true));
        Assert.True(unlocked.IsUnlockedAt(Now));
        Assert.True(unlocked.IsOpenAt(Now, forceLocal: false));
        Assert.False(expired.IsUnlockedAt(Now));
        Assert.False(expired.IsOpenAt(Now, forceLocal: false));
        Assert.Equal(new LocalLoginStatus(LocalLoginOpen: false, SsoVerified: true, UnlockedUntil: null, ForcedLocal: false), verified.ToStatus(Now, forceLocal: false));
        Assert.Equal(new LocalLoginStatus(LocalLoginOpen: true, SsoVerified: true, UnlockedUntil: Now.AddMinutes(30), ForcedLocal: false), unlocked.ToStatus(Now, forceLocal: false));
        Assert.Equal(new LocalLoginStatus(LocalLoginOpen: true, SsoVerified: true, UnlockedUntil: null, ForcedLocal: true), expired.ToStatus(Now, forceLocal: true));
    }



    [Theory]
    [InlineData(0.5, false)]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(1200, true)]
    [InlineData(43200, true)]
    [InlineData(43201, false)]
    public void A_window_is_between_one_minute_and_thirty_days(double minutes, bool accepted)
    {
        var reason = LocalLoginGateRules.Validate(TimeSpan.FromMinutes(minutes));

        Assert.Equal(accepted, reason is null);
        Assert.Equal(TimeSpan.FromMinutes(30), LocalLoginGateRules.DefaultWindow);
        Assert.Null(LocalLoginGateRules.Validate(LocalLoginGateRules.DefaultWindow));
    }



    [Fact]
    public async Task The_placeholder_gate_is_open_and_refuses_to_unlock_or_lock()
    {
        var gate = new NoLocalLoginGate();

        Assert.Equal(LocalLoginGateInfo.Open, await gate.GetAsync(CancellationToken.None));
        Assert.Equal(LocalLoginGateInfo.Open, await gate.MarkSsoVerifiedAsync("oidc", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => gate.MarkSsoVerifiedAsync(" ", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => gate.UnlockAsync(TimeSpan.FromMinutes(5), " ", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => gate.LockAsync("", CancellationToken.None));
        var unlock = await Assert.ThrowsAsync<AuthException>(() => gate.UnlockAsync(TimeSpan.FromMinutes(5), "HOST\\ops", CancellationToken.None));
        var @lock = await Assert.ThrowsAsync<AuthException>(() => gate.LockAsync("HOST\\ops", CancellationToken.None));
        Assert.Equal(AuthErrorCodes.Unavailable, unlock.Code);
        Assert.Equal(AuthErrorCodes.Unavailable, @lock.Code);
    }



    [Fact]
    public void The_module_declares_the_closed_gate_error_code()
    {
        Assert.Contains(AuthErrorCodes.LocalLoginClosed, AuthModule.Descriptor.ErrorCodes);
        Assert.Equal(403, AuthErrorCodes.LocalLoginClosed.HttpStatus);
        Assert.Equal("/auth/local/status", AuthModule.LocalStatusRoute);
    }
}
