// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.UnitTests.Licensing;

/// <summary>
/// E79.4, E79.5: under the ceiling is allowed; over it is allowed within the allowance until grace ends,
/// with the banner; beyond the allowance, after grace, or on lapsed coverage it is blocked with a message
/// naming the limit and the tier; unlimited never blocks.
/// </summary>
public sealed class LimitCheckTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);



    [Fact]
    public void Free_tier_devices_one_over_is_a_warning_with_fourteen_days()
    {
        var free = LicenseComposer.Compose(TierTables.Current, [], Today);

        var under = LimitCheck.Evaluate(free, LicenseLimits.Devices, 5, overageSince: null, Today);
        var oneOver = LimitCheck.Evaluate(free, LicenseLimits.Devices, 6, overageSince: null, Today);
        var twoOver = LimitCheck.Evaluate(free, LicenseLimits.Devices, 7, overageSince: null, Today);
        var late = LimitCheck.Evaluate(free, LicenseLimits.Devices, 6, Today.AddDays(-15), Today);
        var lastDay = LimitCheck.Evaluate(free, LicenseLimits.Devices, 6, Today.AddDays(-14), Today);
        var users = LimitCheck.Evaluate(free, LicenseLimits.Users, int.MaxValue, overageSince: null, Today);

        Assert.Equal((LimitOutcome.Allowed, string.Empty, (DateOnly?)null), (under.Outcome, under.Message, under.GraceEndsOn));
        Assert.Equal((LimitOutcome.WithinAllowance, Today.AddDays(14)), (oneOver.Outcome, oneOver.GraceEndsOn));
        Assert.Equal("6 of 5 connected devices (active in the last 30 days) — 14 days to add licenses.", oneOver.Message);
        Assert.Equal(LimitOutcome.Blocked, twoOver.Outcome);
        Assert.Equal("7 of 5 connected devices (active in the last 30 days): the free tier allows at most 6 while licenses are added; install a larger key or remove one.", twoOver.Message);
        Assert.Equal(LimitOutcome.Blocked, late.Outcome);
        Assert.Equal("6 of 5 connected devices (active in the last 30 days): the grace period ended 2026-09-19; add licenses to the free tier or remove one.", late.Message);
        Assert.Equal((LimitOutcome.WithinAllowance, "6 of 5 connected devices (active in the last 30 days) — 0 days to add licenses."), (lastDay.Outcome, lastDay.Message));
        Assert.Equal(LimitOutcome.Allowed, users.Outcome);
        Assert.Equal((LicenseLimits.Devices, LimitValue.Of(5), 6), (oneOver.Limit, oneOver.Ceiling, oneOver.Count));
        Assert.Throws<ArgumentNullException>(() => LimitCheck.Evaluate(null!, LicenseLimits.Devices, 1, overageSince: null, Today));
        Assert.Throws<ArgumentNullException>(() => LimitCheck.Evaluate(free, null!, 1, overageSince: null, Today));
    }



    [Fact]
    public void Lapsed_coverage_blocks_creation_over_the_limit_and_nothing_else()
    {
        var key = new LicenseKey(1, "b1", LicenseKeyKind.Base, "pro", "Acme", [new CoveragePeriod(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31))], [], new Dictionary<string, LimitValue>(StringComparer.Ordinal), Devices: 0, [], new DateOnly(2025, 1, 1));
        var lapsed = LicenseComposer.Compose(TierTables.Current, [new InstalledKey(key, "b1", KeyStatus.Active, Reason: null)], Today);

        var within = LimitCheck.Evaluate(lapsed, LicenseLimits.Devices, 5, overageSince: null, Today);
        var over = LimitCheck.Evaluate(lapsed, LicenseLimits.Devices, 6, overageSince: null, Today);

        Assert.Equal(LimitOutcome.Allowed, within.Outcome);
        Assert.Equal(LimitOutcome.Blocked, over.Outcome);
        Assert.Equal("The license coverage ended 2025-12-31: no more connected devices (active in the last 30 days) can be added on the pro tier until it is renewed.", over.Message);
        Assert.True(lapsed.HasFeature(LicenseFeatures.WorkspaceInsights));   // nothing is removed
    }
}
