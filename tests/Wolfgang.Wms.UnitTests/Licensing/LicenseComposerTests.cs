// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.UnitTests.Licensing;

/// <summary>
/// E79.1, E79.5, E79.11: the free tier with no keys; a base key resolved through the table with explicit
/// entries winning; add-ons stacking devices, features and raised limits; superseded, foreign, extra and
/// unreadable keys flagged; a lapsed base freezing the stack; an add-on lapsing on its own.
/// </summary>
public sealed class LicenseComposerTests
{
    private static readonly DateOnly Release = new(2026, 9, 20);



    [Fact]
    public void No_keys_is_the_free_tier()
    {
        var license = LicenseComposer.Compose(TierTables.Current, [], Release);

        Assert.Equal(("free", string.Empty, CoverageStatus.Perpetual, (DateOnly?)null), (license.Tier.Name, license.Organization, license.Coverage, license.CoveredUntil));
        Assert.Equal((5, 0, FreeLicense.AllowancePercent, FreeLicense.AllowanceMinimumUnits, FreeLicense.GraceDays), (license.IncludedDevices, license.PurchasedDevices, license.AllowancePercent, license.AllowanceMinimumUnits, license.GraceDays));
        Assert.True(license.HasFeature(LicenseFeatures.Backups));
        Assert.False(license.HasFeature(LicenseFeatures.WorkspaceInsights));
        Assert.Equal(LimitValue.Of(1), license.Limit(LicenseLimits.Sites));
        Assert.Equal(LimitValue.Of(5), license.Limit(LicenseLimits.Devices));
        Assert.True(license.Limit(new Domain.Keys.LicenseLimit("later_limit", "l")).IsUnlimited);
        Assert.Empty(license.Keys);
        Assert.Equal(1, license.AllowanceUnits(LicenseLimits.Devices));   // max(10% of 5 = 1, floor 1)
        Assert.Equal(0, license.AllowanceUnits(LicenseLimits.Users));
        Assert.Throws<ArgumentNullException>(() => license.HasFeature(null!));
        Assert.Throws<ArgumentNullException>(() => license.Limit(null!));
        Assert.Throws<ArgumentNullException>(() => LicenseComposer.Compose(null!, [], Release));
        Assert.Throws<ArgumentNullException>(() => LicenseComposer.Compose(TierTables.Current, null!, Release));
        Assert.True(FreeLicense.Key.IsPerpetual);
        Assert.True(FreeLicense.Key.Covers(new DateOnly(2099, 1, 1)));
    }



    [Fact]
    public void A_base_and_its_add_ons_compose()
    {
        var pro = Base("b1", "pro", "Acme", features: ["reports.custom_views", "not.a.feature"], limits: new Dictionary<string, LimitValue>(StringComparer.Ordinal) { ["sites"] = LimitValue.Of(3), ["later_limit"] = LimitValue.Of(1) }, allowance: 25, minimum: 4, grace: 60);
        var devices = AddOn("a1", "Acme", devices: 10);
        var totes = AddOn("a2", "Acme", limits: new Dictionary<string, LimitValue>(StringComparer.Ordinal) { ["max_totes_per_picker"] = LimitValue.Of(2), ["sites"] = LimitValue.Of(10), ["devices"] = LimitValue.Of(999), ["ghost"] = LimitValue.Of(1) }, features: ["workspace.insights"]);

        var license = LicenseComposer.Compose(TierTables.Current, [Installed(devices), Installed(pro), Installed(totes)], Release);

        Assert.Equal(("pro", "Acme", CoverageStatus.Covered, new DateOnly(2027, 9, 19)), (license.Tier.Name, license.Organization, license.Coverage, license.CoveredUntil));
        Assert.Equal((5, 10, 25, 4, 60), (license.IncludedDevices, license.PurchasedDevices, license.AllowancePercent, license.AllowanceMinimumUnits, license.GraceDays));
        Assert.Equal(LimitValue.Of(15), license.Limit(LicenseLimits.Devices));
        Assert.Equal(LimitValue.Of(10), license.Limit(LicenseLimits.Sites));   // the add-on raised the explicit 3
        Assert.Equal(LimitValue.Of(5), license.Limit(LicenseLimits.MaxTotesPerPicker));   // the add-on's 2 never lowers the tier's 5
        Assert.True(license.HasFeature(LicenseFeatures.WorkspaceInsights));
        Assert.True(license.HasFeature(LicenseFeatures.ReportsCustomViews));
        Assert.DoesNotContain("not.a.feature", license.Features);
        Assert.All(license.Keys, k => Assert.Equal(KeyStatus.Active, k.Status));
        Assert.Equal(["a1", "b1", "a2"], license.Keys.Select(k => k.KeyId));
        Assert.Equal(4, license.AllowanceUnits(LicenseLimits.Devices));   // max(ceil(15 * 25%) = 4, floor 4)
    }



    [Fact]
    public void Superseded_foreign_extra_and_unreadable_keys_are_flagged()
    {
        var older = Base("b0", "pro", "Acme", issued: new DateOnly(2026, 1, 1));
        var newer = Base("b1", "enterprise", "Acme", issued: new DateOnly(2026, 6, 1), supersedes: ["a0"]);
        var replaced = AddOn("a0", "Acme", devices: 5);
        var foreign = AddOn("a1", "Other Co", devices: 5);
        var lapsedAddOn = AddOn("a2", "Acme", devices: 5, coverage: [new CoveragePeriod(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31))]);
        var unreadable = new InstalledKey(Key: null, "line 6", KeyStatus.Invalid, "the signature does not verify");
        var unreadableNoReason = new InstalledKey(Key: null, "line 7", KeyStatus.Active, Reason: null);

        var license = LicenseComposer.Compose(TierTables.Current, [Installed(older), Installed(newer), Installed(replaced), Installed(foreign), Installed(lapsedAddOn), unreadable, unreadableNoReason], Release);
        var statuses = license.Keys.ToDictionary(k => k.KeyId, k => (k.Status, k.Reason), StringComparer.Ordinal);

        Assert.Equal("enterprise", license.Tier.Name);
        Assert.Equal(0, license.PurchasedDevices);
        Assert.Equal((KeyStatus.ExtraBase, "a newer base key (b1) is in force; one base counts"), statuses["b0"]);
        Assert.Equal((KeyStatus.Active, (string?)null), statuses["b1"]);
        Assert.Equal((KeyStatus.Superseded, "replaced by a later key"), statuses["a0"]);
        Assert.Equal((KeyStatus.ForeignOrganization, "issued to 'Other Co', not 'Acme'"), statuses["a1"]);
        Assert.Equal((KeyStatus.Lapsed, "its coverage ended 2025-12-31"), statuses["a2"]);
        Assert.Equal((KeyStatus.Invalid, "the signature does not verify"), statuses["line 6"]);
        Assert.Equal((KeyStatus.Invalid, "the key could not be verified"), statuses["line 7"]);
    }



    [Fact]
    public void A_lapsed_base_freezes_the_stack_and_add_ons_stack_on_free()
    {
        var lapsed = Base("b1", "pro", "Acme", coverage: [new CoveragePeriod(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31))]);
        var addOn = AddOn("a1", "Acme", devices: 10);

        var frozen = LicenseComposer.Compose(TierTables.Current, [Installed(lapsed), Installed(addOn)], Release);
        var onFree = LicenseComposer.Compose(TierTables.Current, [Installed(addOn)], Release);
        var unknownTier = LicenseComposer.Compose(TierTables.Current, [Installed(Base("b2", "platinum", "Acme"))], Release);

        Assert.Equal((CoverageStatus.Lapsed, new DateOnly(2025, 12, 31), "pro"), (frozen.Coverage, frozen.CoveredUntil, frozen.Tier.Name));
        Assert.Equal(KeyStatus.Lapsed, frozen.Keys[0].Status);
        Assert.Equal("coverage ended 2025-12-31; this release (2026-09-20) is not covered", frozen.Keys[0].Reason);
        Assert.Equal((KeyStatus.Lapsed, "the base key's coverage lapsed; the stack is frozen together"), (frozen.Keys[1].Status, frozen.Keys[1].Reason));
        Assert.Equal(0, frozen.PurchasedDevices);
        Assert.Equal(("free", 10, 15), (onFree.Tier.Name, onFree.PurchasedDevices, onFree.Limit(LicenseLimits.Devices).Value));
        Assert.Equal("free", unknownTier.Tier.Name);   // a tier this release does not know resolves through the free definition
    }



    private static InstalledKey Installed(LicenseKey key)
    {
        return new InstalledKey(key, key.KeyId, KeyStatus.Active, Reason: null);
    }



    private static LicenseKey Base(string id, string tier, string organization, IReadOnlyList<CoveragePeriod>? coverage = null, IReadOnlyList<string>? features = null, IReadOnlyDictionary<string, LimitValue>? limits = null, DateOnly? issued = null, IReadOnlyList<string>? supersedes = null, int? allowance = null, int? minimum = null, int? grace = null)
    {
        return new LicenseKey(1, id, LicenseKeyKind.Base, tier, organization, coverage ?? [new CoveragePeriod(new DateOnly(2026, 9, 20), new DateOnly(2027, 9, 19))], features ?? [], limits ?? new Dictionary<string, LimitValue>(StringComparer.Ordinal), Devices: 0, supersedes ?? [], issued ?? new DateOnly(2026, 9, 1), allowance, minimum, grace);
    }



    private static LicenseKey AddOn(string id, string organization, int devices = 0, IReadOnlyList<string>? features = null, IReadOnlyDictionary<string, LimitValue>? limits = null, IReadOnlyList<CoveragePeriod>? coverage = null)
    {
        return new LicenseKey(1, id, LicenseKeyKind.AddOn, Tier: null, organization, coverage ?? [], features ?? [], limits ?? new Dictionary<string, LimitValue>(StringComparer.Ordinal), devices, [], new DateOnly(2026, 9, 2));
    }
}
