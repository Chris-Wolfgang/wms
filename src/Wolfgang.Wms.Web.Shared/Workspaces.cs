// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Web.Shared;

/// <summary>
/// The five console workspaces (E82.4), defined once. Each is a license feature (<c>workspace.&lt;name&gt;</c>)
/// and a permission (<c>workspace.&lt;name&gt;.enter</c>); the v1 free tier includes Configure, Supervise,
/// Resolve and Report, and Insights is paid from the start.
/// </summary>
public static class Workspaces
{
    /// <summary>
    /// Administrators: sites, zones, settings, users, licenses.
    /// </summary>
    public static Workspace Configure { get; } = Define("configure", "Configure", freeTier: true);



    /// <summary>
    /// Live operations: releases, devices, exceptions as they happen.
    /// </summary>
    public static Workspace Supervise { get; } = Define("supervise", "Supervise", freeTier: true);



    /// <summary>
    /// The tote resolution lane; touch-first and tablet-friendly, paired with the handheld's Resolver mode.
    /// </summary>
    public static Workspace Resolve { get; } = Define("resolve", "Resolve", freeTier: true);



    /// <summary>
    /// What is happening: rates, totes on the line, time to completion, device and bin metrics. Read-only.
    /// </summary>
    public static Workspace Report { get; } = Define("report", "Report", freeTier: true);



    /// <summary>
    /// How to improve: divert and skip findings, mis-slotted products, recommendations. Paid.
    /// </summary>
    public static Workspace Insights { get; } = Define("insights", "Insights", freeTier: false);



    /// <summary>
    /// Every workspace, in navigation order.
    /// </summary>
    public static IReadOnlyList<Workspace> All { get; } = [Configure, Supervise, Resolve, Report, Insights];



    // The English name only labels the license feature and permission (domain metadata, like every key
    // description); what users see comes from ConsoleText.
    private static Workspace Define(string name, string title, bool freeTier)
    {
        return new Workspace
        (
            name,
            new LicenseFeature("workspace." + name, $"The {title} console workspace"),
            new Permission("workspace." + name + ".enter", $"Enter the {title} workspace"),
            freeTier
        );
    }
}
