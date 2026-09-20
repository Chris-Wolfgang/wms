// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Reflection;

namespace Wolfgang.Wms.Web;

/// <summary>
/// The workspace assemblies the console host routes to (E82.4). Adding a workspace is one project reference
/// and one entry here; splitting one into its own host later removes the entry and changes no component.
/// </summary>
public static class WorkspaceAssemblies
{
    /// <summary>
    /// Every workspace assembly, in navigation order.
    /// </summary>
    public static IReadOnlyList<Assembly> All { get; } =
    [
        typeof(Configure.ConfigureLayout).Assembly,
        typeof(Supervise.SuperviseLayout).Assembly,
        typeof(Resolve.ResolveLayout).Assembly,
        typeof(Report.ReportLayout).Assembly,
        typeof(Insights.InsightsLayout).Assembly,
    ];
}
