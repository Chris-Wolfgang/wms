// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.UnitTests.Licensing;

/// <summary>
/// E79.1, E79.2: this release's tier table is complete and monotonic; the free tier names every feature
/// individually with the story's limits; a broken table is reported line by line; the cross-release checks
/// catch removals and reductions and list additions; the line form round-trips.
/// </summary>
public sealed class TierTableTests
{
    private static readonly Dictionary<string, LimitValue> AllLimits = LicenseLimits.All.ToDictionary(l => l.Name, _ => LimitValue.Of(1), StringComparer.Ordinal);



    [Fact]
    public void The_current_table_is_complete_and_monotonic()
    {
        var table = TierTables.Current;
        var free = table.Find(LicenseTiers.Free)!;

        Assert.Empty(table.Verify());
        Assert.Equal(ReleaseInfo.Version, table.Version);
        Assert.Equal(["free", "pro", "enterprise"], table.Tiers.Select(t => t.Tier.Name));
        Assert.Equal((1, 5, true, 1), (free.Limit(LicenseLimits.Sites).Value, free.Limit(LicenseLimits.Devices).Value, free.Limit(LicenseLimits.Users).IsUnlimited, free.Limit(LicenseLimits.MaxTotesPerPicker).Value));
        Assert.Equal(TierTables.FreeFeatures.Select(f => f.Name).Order(StringComparer.Ordinal), free.Features.Order(StringComparer.Ordinal));
        Assert.All(TierTables.PaidFeatures, paid => Assert.False(free.Grants(paid)));
        Assert.Equal(LicenseFeatures.All.Select(f => f.Name).Order(StringComparer.Ordinal), TierTables.FreeFeatures.Concat(TierTables.PaidFeatures).Select(f => f.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["workspace.insights", "reports.custom_views", "picking.bulk", "messaging.picker_to_picker", "devices.remote_logging", "devices.bulk_enrollment"], TierTables.PaidFeatures.Select(f => f.Name));
        Assert.Null(table.Find(null));
        Assert.Null(table.Find(new LicenseTier("platinum", 9)));
        Assert.Equal(LicenseLimits.Sites, LicenseLimits.Find("sites"));
        Assert.Null(LicenseLimits.Find("nope"));
        Assert.Null(LicenseFeatures.Find(null));
        Assert.Equal(LicenseTiers.Pro, LicenseTiers.Find("pro"));
        Assert.Null(LicenseTiers.Find("gold"));
    }



    [Fact]
    public void A_broken_table_is_reported_problem_by_problem()
    {
        var lower = new TierDefinition(LicenseTiers.Free, [LicenseFeatures.Backups, new LicenseFeature("ghost.feature", "g")], new Dictionary<string, LimitValue>(AllLimits, StringComparer.Ordinal) { [LicenseLimits.Sites.Name] = LimitValue.Of(3), ["ghost_limit"] = LimitValue.Of(1) });
        var higher = new TierDefinition(LicenseTiers.Pro, [], new Dictionary<string, LimitValue>(StringComparer.Ordinal) { [LicenseLimits.Sites.Name] = LimitValue.Of(2) });
        var table = new TierTable("9.9.9", [lower, higher]);

        var problems = table.Verify();

        Assert.Contains("tier 'enterprise' has no definition in 9.9.9", problems);
        Assert.Contains("tier 'pro' has no value for limit 'devices'", problems);
        Assert.Contains("tier 'free' values unknown limit 'ghost_limit'", problems);
        Assert.Contains("tier 'free' grants unknown feature 'ghost.feature'", problems);
        Assert.Contains("tier 'pro' lacks 'backups.core' which the lower tier 'free' grants", problems);
        Assert.Contains("tier 'pro' values 'sites' below the lower tier 'free' (2 < 3)", problems);
        Assert.Throws<ArgumentException>(() => new TierTable(" ", []));
        Assert.Throws<ArgumentNullException>(() => new TierTable("1", null!));
        Assert.Throws<ArgumentNullException>(() => new TierDefinition(null!, [], AllLimits));
        Assert.Throws<ArgumentNullException>(() => new TierDefinition(LicenseTiers.Free, null!, AllLimits));
        Assert.Throws<ArgumentNullException>(() => new TierDefinition(LicenseTiers.Free, [], null!));
        Assert.Throws<ArgumentNullException>(() => lower.Grants(null!));
        Assert.Throws<ArgumentNullException>(() => lower.Limit(null!));
        Assert.True(higher.Limit(LicenseLimits.Devices).IsUnlimited);   // an unvalued limit reads as unlimited; Verify is what forbids it
    }



    [Fact]
    public void Regressions_and_additions_between_releases()
    {
        var previous = new TierTable("0.1.0",
        [
            new TierDefinition(LicenseTiers.Free, [LicenseFeatures.Backups, LicenseFeatures.Issues], new Dictionary<string, LimitValue>(AllLimits, StringComparer.Ordinal) { [LicenseLimits.Sites.Name] = LimitValue.Of(2), [LicenseLimits.Users.Name] = LimitValue.Unlimited }),
            new TierDefinition(LicenseTiers.Pro, [LicenseFeatures.Backups, LicenseFeatures.Issues], AllLimits),
        ]);
        var current = new TierTable("0.2.0",
        [
            new TierDefinition(LicenseTiers.Free, [LicenseFeatures.Backups, LicenseFeatures.SelfUpdate], new Dictionary<string, LimitValue>(StringComparer.Ordinal) { [LicenseLimits.Sites.Name] = LimitValue.Of(1), [LicenseLimits.Devices.Name] = LimitValue.Of(5), [LicenseLimits.Users.Name] = LimitValue.Of(3) }),
            new TierDefinition(LicenseTiers.Enterprise, [LicenseFeatures.Backups], AllLimits),
        ]);

        var regressions = TierTable.Regressions(previous, current);
        var additions = TierTable.Additions(previous, current);

        Assert.Equal(["tier 'free' lost feature 'issues.core' in 0.2.0", "tier 'free' limit 'sites' went from 2 to 1 in 0.2.0", "tier 'free' limit 'users' went from unlimited to 3 in 0.2.0", "tier 'pro' was removed in 0.2.0"], regressions);
        Assert.Equal(["tier 'free' gains 'self_update'", "tier 'free' limit 'devices' raised from 1 to 5", "tier 'enterprise' added"], additions);
        Assert.Empty(TierTable.Regressions(TierTables.Current, TierTables.Current));
        Assert.Empty(TierTable.Additions(TierTables.Current, TierTables.Current));
        Assert.Throws<ArgumentNullException>(() => TierTable.Regressions(null!, current));
        Assert.Throws<ArgumentNullException>(() => TierTable.Regressions(previous, null!));
        Assert.Throws<ArgumentNullException>(() => TierTable.Additions(null!, current));
        Assert.Throws<ArgumentNullException>(() => TierTable.Additions(previous, null!));
    }



    [Fact]
    public void Lines_round_trip_and_reject_other_shapes()
    {
        var lines = TierTables.Current.ToLines();
        var back = TierTable.FromLines("x", lines);
        var foreign = TierTable.FromLines("y", ["gold|feature|future.thing|granted", "gold|limit|sites|unlimited", " ", "free|limit|sites|7"]);

        Assert.Equal(lines, back.ToLines());
        Assert.Equal("free|feature|auth.local|granted", lines[0]);
        Assert.Contains("enterprise|limit|max_totes_per_picker|unlimited", lines);
        Assert.Contains("free|limit|sites|1", lines);
        Assert.Equal(["free", "gold"], foreign.Tiers.Select(t => t.Tier.Name));
        Assert.Equal(int.MaxValue, foreign.Tiers[1].Tier.Rank);
        Assert.True(foreign.Tiers[1].Features.Contains("future.thing"));
        Assert.True(foreign.Tiers[1].Limit(LicenseLimits.Sites).IsUnlimited);
        Assert.Equal(7, foreign.Tiers[0].Limit(LicenseLimits.Sites).Value);
        Assert.Throws<FormatException>(() => TierTable.FromLines("z", ["free|sites"]));
        Assert.Throws<FormatException>(() => TierTable.FromLines("z", ["free|other|sites|1"]));
        Assert.Throws<ArgumentNullException>(() => TierTable.FromLines("z", null!));
    }



    [Fact]
    public void Limit_values_compare_and_print()
    {
        Assert.Equal("unlimited", LimitValue.Unlimited.ToString());
        Assert.Equal("5", LimitValue.Of(5).ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => LimitValue.Of(-1));
        Assert.Equal(LimitValue.Of(8), LimitValue.Of(5).Plus(3));
        Assert.True(LimitValue.Unlimited.Plus(3).IsUnlimited);
        Assert.Equal(LimitValue.Of(2), LimitValue.Min(LimitValue.Of(2), LimitValue.Of(5)));
        Assert.Equal(LimitValue.Of(2), LimitValue.Min(LimitValue.Of(5), LimitValue.Of(2)));
        Assert.Equal(LimitValue.Of(2), LimitValue.Min(LimitValue.Unlimited, LimitValue.Of(2)));
        Assert.Equal(LimitValue.Of(2), LimitValue.Min(LimitValue.Of(2), LimitValue.Unlimited));
        Assert.True(LimitValue.Of(5).IsReducedBy(LimitValue.Of(4)));
        Assert.True(LimitValue.Unlimited.IsReducedBy(LimitValue.Of(4)));
        Assert.False(LimitValue.Of(5).IsReducedBy(LimitValue.Unlimited));
        Assert.False(LimitValue.Of(5).IsReducedBy(LimitValue.Of(5)));
        Assert.True(LimitValue.Of(5).IsExceededBy(6));
        Assert.False(LimitValue.Of(5).IsExceededBy(5));
        Assert.False(LimitValue.Unlimited.IsExceededBy(int.MaxValue));
        Assert.True(new CoveragePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)).Contains(new DateOnly(2026, 6, 1)));
        Assert.False(new CoveragePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)).Contains(new DateOnly(2027, 1, 1)));
    }
}
