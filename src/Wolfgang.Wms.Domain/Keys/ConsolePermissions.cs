// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// The permissions that gate the console workspaces (E82.4, E10.1): one <c>workspace.&lt;name&gt;.enter</c>
/// per workspace, defined here so the console (which checks them) and the API (which lists and grants them)
/// share one spelling.
/// </summary>
public static class ConsolePermissions
{
    /// <summary>Enter the Configure workspace.</summary>
    public static readonly Permission EnterConfigure = For("configure", "Configure");

    /// <summary>Enter the Supervise workspace.</summary>
    public static readonly Permission EnterSupervise = For("supervise", "Supervise");

    /// <summary>Enter the Resolve workspace.</summary>
    public static readonly Permission EnterResolve = For("resolve", "Resolve");

    /// <summary>Enter the Report workspace.</summary>
    public static readonly Permission EnterReport = For("report", "Report");

    /// <summary>Enter the Insights workspace.</summary>
    public static readonly Permission EnterInsights = For("insights", "Insights");



    /// <summary>
    /// Every workspace permission.
    /// </summary>
    public static IReadOnlyList<Permission> All { get; } = [EnterConfigure, EnterSupervise, EnterResolve, EnterReport, EnterInsights];



    /// <summary>
    /// The entry permission of the workspace named <paramref name="name"/> (the registered instance when
    /// there is one).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or blank.</exception>
    public static Permission For(string name, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var permissionName = "workspace." + name + ".enter";
        return All?.FirstOrDefault(p => string.Equals(p.Name, permissionName, StringComparison.Ordinal)) ?? new Permission(permissionName, $"Enter the {title} workspace");
    }
}
