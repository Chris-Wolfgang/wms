// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Reflection;
using Microsoft.AspNetCore.Components;
using Wolfgang.Wms.Web;
using Wolfgang.Wms.Web.Configure;
using Wolfgang.Wms.Web.Shared.Components;

namespace Wolfgang.Wms.UnitTests.Architecture;

/// <summary>
/// The workspace entry gate (license and permission, checked by <see cref="WorkspaceLayout"/>) is not opt-in per
/// page: every routable component in a workspace assembly renders under a <see cref="WorkspaceLayout"/>, which
/// each workspace's <c>Pages/_Imports.razor</c> provides. A page that named another layout, or none, would be
/// reachable by an unlicensed or unpermitted user, so the gate must fail closed.
/// </summary>
public sealed class WorkspacePageGateTests
{
    [Fact]
    public void Every_routable_workspace_component_renders_under_a_WorkspaceLayout()
    {
        var types = WorkspaceAssemblies.All.SelectMany(a => a.GetTypes()).ToArray();

        Assert.NotEmpty(RoutableComponentsIn(types));
        Assert.Empty(UngatedPagesIn(types));
    }



    [Fact]
    public void Ungated_scan_reports_a_page_without_a_layout_and_a_page_under_a_layout_that_is_not_a_workspace_layout()
    {
        var ungated = UngatedPagesIn([typeof(GatedSample), typeof(OpenSample), typeof(NoLayoutSample), typeof(NotAPage)]);

        Assert.Equal([typeof(NoLayoutSample).FullName!, typeof(OpenSample).FullName!], ungated);
    }



    private static Type[] RoutableComponentsIn(IEnumerable<Type> types)
    {
        return types.Where(t => t.GetCustomAttributes<RouteAttribute>().Any()).ToArray();
    }



    private static string[] UngatedPagesIn(IEnumerable<Type> types)
    {
        return RoutableComponentsIn(types)
            .Where(t => !typeof(WorkspaceLayout).IsAssignableFrom(t.GetCustomAttribute<LayoutAttribute>()?.LayoutType))
            .Select(t => t.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }



    [Route("/sample/gated")]
    [Layout(typeof(ConfigureLayout))]
    private sealed class GatedSample : ComponentBase
    {
    }



    [Route("/sample/open")]
    [Layout(typeof(OpenLayout))]
    private sealed class OpenSample : ComponentBase
    {
    }



    [Route("/sample/none")]
    private sealed class NoLayoutSample : ComponentBase
    {
    }



    private sealed class NotAPage : ComponentBase
    {
    }



    private sealed class OpenLayout : LayoutComponentBase
    {
    }
}
