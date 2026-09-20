// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Web.Shared;

namespace Wolfgang.Wms.UnitTests.Web;

public sealed class WorkspacesTests
{
    [Fact]
    public void Five_workspaces_in_navigation_order_with_unique_routes()
    {
        Assert.Equal(["configure", "supervise", "resolve", "report", "insights"], Workspaces.All.Select(w => w.Name));
        Assert.Equal(["/configure", "/supervise", "/resolve", "/report", "/insights"], Workspaces.All.Select(w => w.Route));
        Assert.Equal(Workspaces.All.Count, Workspaces.All.Select(w => w.Route).Distinct(StringComparer.Ordinal).Count());
    }



    [Fact]
    public void Each_workspace_is_a_license_feature_and_a_permission_named_after_it()
    {
        Assert.All(Workspaces.All, w =>
        {
            Assert.Equal("workspace." + w.Name, w.LicenseFeature.Name);
            Assert.Equal("workspace." + w.Name + ".enter", w.Permission.Name);
            Assert.False(string.IsNullOrWhiteSpace(w.Description));
        });
    }



    [Fact]
    public void Free_tier_is_everything_but_Insights()
    {
        Assert.Equal(["configure", "supervise", "resolve", "report"], Workspaces.All.Where(w => w.FreeTier).Select(w => w.Name));
        Assert.False(Workspaces.Insights.FreeTier);
    }



    [Fact]
    public void Workspace_name_is_validated_as_a_key()
    {
        var feature = new LicenseFeature("workspace.x", "x");
        var permission = new Permission("workspace.x.enter", "x");

        Assert.Throws<ArgumentException>(() => new Workspace("Not A Key", "T", "D", feature, permission, FreeTier: true));
    }



    [Fact]
    public async Task FreeTierWorkspaceAccess_allows_free_workspaces_and_reports_paid_ones_as_not_licensed()
    {
        IWorkspaceAccess access = new FreeTierWorkspaceAccess();

        Assert.Equal(WorkspaceAccessResult.Allowed, await access.CheckAsync(Workspaces.Configure, CancellationToken.None));
        Assert.Equal(WorkspaceAccessResult.NotLicensed, await access.CheckAsync(Workspaces.Insights, CancellationToken.None));
        Assert.Equal(["configure", "supervise", "resolve", "report"], (await access.EnterableAsync(Workspaces.All, CancellationToken.None)).Select(w => w.Name));
        await Assert.ThrowsAsync<ArgumentNullException>(() => access.CheckAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => access.EnterableAsync(null!, CancellationToken.None));
    }
}
