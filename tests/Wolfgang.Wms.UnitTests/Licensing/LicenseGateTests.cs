// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Licensing;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.UnitTests.Licensing;

/// <summary>
/// E79.4, E79.8: the gate recounts from the data, records the day a limit first went over, clears it when
/// the count drops back, and refuses a blocked creation with the <c>license.limit_reached</c> problem.
/// </summary>
public sealed class LicenseGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);



    [Fact]
    public async Task Over_the_limit_is_recorded_then_cleared_and_blocked_after_grace()
    {
        using var keys = new TestLicenseKeys();
        var settings = new FakeLicenseSettings();
        var usage = new FakeUsage();
        using var provider = LicenseStateTests.Host(settings, keys.Verifier());
        var time = new TestClock(Now);
        var gate = new LicenseGate(provider.GetRequiredService<ILicense>(), usage, settings, time);

        usage.Counts[LicenseLimits.Devices.Name] = 4;
        var allowed = await gate.CheckAsync(LicenseLimits.Devices, 1, CancellationToken.None);
        var withinNext = await gate.CheckAsync(LicenseLimits.Devices, 2, CancellationToken.None);
        var overNext = await gate.CheckAsync(LicenseLimits.Devices, 3, CancellationToken.None);
        var underStanding = await gate.ReconcileAsync(LicenseLimits.Devices, "tester", CancellationToken.None);
        usage.Counts[LicenseLimits.Devices.Name] = 6;
        var firstDayOver = await gate.ReconcileAsync(LicenseLimits.Devices, "tester", CancellationToken.None);
        time.Advance(TimeSpan.FromDays(10));
        var tenDaysLater = await gate.ReconcileAsync(LicenseLimits.Devices, "tester", CancellationToken.None);
        time.Advance(TimeSpan.FromDays(5));
        var afterGrace = await gate.CheckAsync(LicenseLimits.Devices, 0, CancellationToken.None);
        var refused = await Assert.ThrowsAsync<LicenseException>(() => gate.RequireAsync(LicenseLimits.Devices, 0, CancellationToken.None));
        usage.Counts[LicenseLimits.Devices.Name] = 5;
        var cleared = await gate.ReconcileAsync(LicenseLimits.Devices, "tester", CancellationToken.None);
        var passed = await gate.RequireAsync(LicenseLimits.Devices, 0, CancellationToken.None);

        Assert.Equal(LimitOutcome.Allowed, allowed.Outcome);
        Assert.Equal(LimitOutcome.WithinAllowance, withinNext.Outcome);   // 6 of 5 is within the free allowance (10%, floor 1)
        Assert.Equal(LimitOutcome.Blocked, overNext.Outcome);   // 7 of 5 is beyond it
        Assert.Equal(LimitOutcome.Allowed, underStanding.Outcome);
        Assert.Equal((LimitOutcome.WithinAllowance, new DateOnly(2026, 10, 4)), (firstDayOver.Outcome, firstDayOver.GraceEndsOn));
        Assert.Equal((LimitOutcome.WithinAllowance, new DateOnly(2026, 10, 4), "6 of 5 connected devices (active in the last 30 days) — 4 days to add licenses."), (tenDaysLater.Outcome, tenDaysLater.GraceEndsOn, tenDaysLater.Message));
        Assert.Equal(LimitOutcome.Blocked, afterGrace.Outcome);
        Assert.Equal((LicenseErrorCodes.LimitReached, afterGrace.Message), (refused.Code, refused.Message));
        Assert.Equal(LimitOutcome.Allowed, cleared.Outcome);
        Assert.Equal(LimitOutcome.Allowed, passed.Outcome);
        Assert.Equal(["license.overage_since.devices=2026-09-20T12:00:00.0000000+00:00 by tester", "license.overage_since.devices=1970-01-01T00:00:00.0000000+00:00 by tester"], settings.Writes);
        await Assert.ThrowsAsync<ArgumentNullException>(() => gate.CheckAsync(null!, 0, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => gate.ReconcileAsync(null!, "u", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => gate.ReconcileAsync(LicenseLimits.Devices, null!, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => new LicenseGate(null!, usage, settings, time));
        Assert.Throws<ArgumentNullException>(() => new LicenseGate(provider.GetRequiredService<ILicense>(), null!, settings, time));
        Assert.Throws<ArgumentNullException>(() => new LicenseGate(provider.GetRequiredService<ILicense>(), usage, null!, time));
        Assert.Throws<ArgumentNullException>(() => new LicenseGate(provider.GetRequiredService<ILicense>(), usage, settings, null!));
    }



    [Fact]
    public async Task The_exception_carries_its_code_and_the_handler_answers_with_it()
    {
        var handler = new LicenseExceptionHandler();
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider() };
        context.Response.Body = new MemoryStream();
        var inner = new InvalidOperationException("cause");
        var placeholder = new NoLicenseUsage();

        var handled = await handler.TryHandleAsync(context, new LicenseException(LicenseErrorCodes.LimitReached, "7 of 5 devices"), CancellationToken.None);
        var ignored = await handler.TryHandleAsync(context, inner, CancellationToken.None);
        var withCause = new LicenseException(LicenseErrorCodes.KeyRejected, "bad", inner);

        Assert.True(handled);
        Assert.Equal(403, context.Response.StatusCode);
        Assert.False(ignored);
        Assert.Same(inner, withCause.InnerException);
        Assert.Equal("bad", withCause.Message);
        Assert.Throws<ArgumentNullException>(() => new LicenseException(null!, "d"));
        Assert.Throws<ArgumentNullException>(() => new LicenseException(null!, "d", inner));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await handler.TryHandleAsync(null!, inner, CancellationToken.None));
        Assert.Equal(0, await placeholder.CountAsync(LicenseLimits.Sites, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => placeholder.CountAsync(null!, CancellationToken.None));
        Assert.Equal(4, LicenseErrorCodes.All.Count);
    }



    private sealed class FakeUsage : ILicenseUsage
    {
        public Dictionary<string, int> Counts { get; } = new(StringComparer.Ordinal);

        public Task<int> CountAsync(LicenseLimit limit, CancellationToken cancellationToken)
        {
            return Task.FromResult(Counts.GetValueOrDefault(limit.Name));
        }
    }
}
