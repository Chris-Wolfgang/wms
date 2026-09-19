// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.UnitTests.Modules;

/// <summary>
/// E1.13: modules contribute their keys through the descriptor as typed values, accumulated in order.
/// </summary>
public sealed class ModuleDescriptorContributionsTests
{
    private static readonly JobName Retention = new("picking.retention", "Purge closed totes");
    private static readonly JobName Recount = new("picking.recount", "Recount usage");
    private static readonly SettingKey<int> MaxTotes = new("picking.max_totes", 1, "Totes a picker may carry");
    private static readonly Permission Release = new("picking.release", "Release a wave");
    private static readonly FeatureFlag Bulk = new("bulk_picking");
    private static readonly LicenseFeature BulkFeature = new("picking.bulk", "Bulk picking");
    private static readonly IssueType ShortPick = new("picking.short_pick", "A pick came up short");
    private static readonly ErrorCode ToteClosed = new("picking.tote_already_closed", 409, "Tote {tote} is closed.", "tote-closed", ErrorSeverity.Error);



    [Fact]
    public void Contributions_accumulate_in_order_and_leave_the_original_untouched()
    {
        var original = ModuleDescriptor.Create("Picking");

        var picking = original
            .WithJobs(Retention)
            .WithJobs(Recount)
            .WithSettings(MaxTotes)
            .WithPermissions(Release)
            .WithFeatureFlags(Bulk)
            .WithLicenseFeatures(BulkFeature)
            .WithIssueTypes(ShortPick)
            .WithErrorCodes(ToteClosed);

        Assert.Equal([Retention, Recount], picking.Jobs);
        Assert.Equal([MaxTotes], picking.Settings);
        Assert.Equal([Release], picking.Permissions);
        Assert.Equal([Bulk], picking.FeatureFlags);
        Assert.Equal([BulkFeature], picking.LicenseFeatures);
        Assert.Equal([ShortPick], picking.IssueTypes);
        Assert.Equal([ToteClosed], picking.ErrorCodes);
        Assert.Empty(original.Jobs);
        Assert.Empty(original.ErrorCodes);
    }



    [Fact]
    public void A_fresh_descriptor_has_no_contributions()
    {
        var descriptor = ModuleDescriptor.Create("Picking");

        Assert.Empty(descriptor.EndpointMappers);
        Assert.Empty(descriptor.Jobs);
        Assert.Empty(descriptor.Settings);
        Assert.Empty(descriptor.Permissions);
        Assert.Empty(descriptor.FeatureFlags);
        Assert.Empty(descriptor.LicenseFeatures);
        Assert.Empty(descriptor.IssueTypes);
        Assert.Empty(descriptor.ErrorCodes);
    }



    [Fact]
    public void Null_contribution_lists_and_null_items_are_rejected()
    {
        var descriptor = ModuleDescriptor.Create("Picking");

        Assert.Throws<ArgumentNullException>(() => descriptor.WithJobs(null!));
        Assert.Throws<ArgumentException>(() => descriptor.WithPermissions(Release, null!));
    }
}
