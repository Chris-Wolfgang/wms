// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// Every license feature of the product (E79.1, E79.2): each capability that a tier grants is named here,
/// individually, so a license always resolves to an explicit set — "empty means everything" never occurs.
/// A feature added in a later release joins this list and the release's tier table.
/// </summary>
public static class LicenseFeatures
{
    /// <summary>Picking: releases, totes, confirmations.</summary>
    public static LicenseFeature PickingCore { get; } = new("picking.core", "Picking: releases, totes and confirmations");

    /// <summary>Chase mode.</summary>
    public static LicenseFeature PickingChaseMode { get; } = new("picking.chase_mode", "Chase mode");

    /// <summary>Shorts and amendments.</summary>
    public static LicenseFeature PickingShortsAmendments { get; } = new("picking.shorts_amendments", "Shorts and amendments");

    /// <summary>Bulk picking (paid, E91).</summary>
    public static LicenseFeature PickingBulk { get; } = new("picking.bulk", "Bulk picking");

    /// <summary>The Configure workspace.</summary>
    public static LicenseFeature WorkspaceConfigure { get; } = new("workspace.configure", "The Configure console workspace");

    /// <summary>The Supervise workspace.</summary>
    public static LicenseFeature WorkspaceSupervise { get; } = new("workspace.supervise", "The Supervise console workspace");

    /// <summary>The Resolve workspace.</summary>
    public static LicenseFeature WorkspaceResolve { get; } = new("workspace.resolve", "The Resolve console workspace");

    /// <summary>The Report workspace.</summary>
    public static LicenseFeature WorkspaceReport { get; } = new("workspace.report", "The Report console workspace");

    /// <summary>The Insights workspace (paid).</summary>
    public static LicenseFeature WorkspaceInsights { get; } = new("workspace.insights", "The Insights console workspace");

    /// <summary>Built-in reports.</summary>
    public static LicenseFeature ReportsBuiltIn { get; } = new("reports.builtin", "Built-in reports");

    /// <summary>Custom report views (paid, E103).</summary>
    public static LicenseFeature ReportsCustomViews { get; } = new("reports.custom_views", "Custom report views");

    /// <summary>Supervisor to picker messaging.</summary>
    public static LicenseFeature MessagingSupervisorPicker { get; } = new("messaging.supervisor_picker", "Supervisor and picker messaging");

    /// <summary>Picker to picker messaging (paid, E60.3).</summary>
    public static LicenseFeature MessagingPickerToPicker { get; } = new("messaging.picker_to_picker", "Picker-to-picker messaging");

    /// <summary>E-mail notifications.</summary>
    public static LicenseFeature NotificationsEmail { get; } = new("notifications.email", "E-mail notifications");

    /// <summary>Issues.</summary>
    public static LicenseFeature Issues { get; } = new("issues.core", "Issues");

    /// <summary>Every connector format.</summary>
    public static LicenseFeature ConnectorsAllFormats { get; } = new("connectors.all_formats", "Every connector format");

    /// <summary>OpenID Connect sign-in.</summary>
    public static LicenseFeature AuthOidc { get; } = new("auth.oidc", "OpenID Connect sign-in");

    /// <summary>Local accounts.</summary>
    public static LicenseFeature AuthLocal { get; } = new("auth.local", "Local accounts");

    /// <summary>Backups.</summary>
    public static LicenseFeature Backups { get; } = new("backups.core", "Backups");

    /// <summary>Self-update.</summary>
    public static LicenseFeature SelfUpdate { get; } = new("self_update", "Self-update");

    /// <summary>Single-use device enrollment.</summary>
    public static LicenseFeature DevicesSingleUseEnrollment { get; } = new("devices.single_use_enrollment", "Single-use device enrollment");

    /// <summary>Bulk device enrollment (paid, E21.1).</summary>
    public static LicenseFeature DevicesBulkEnrollment { get; } = new("devices.bulk_enrollment", "Bulk device enrollment");

    /// <summary>Push a log level to devices and upload device logs (paid, E12.4).</summary>
    public static LicenseFeature DevicesRemoteLogging { get; } = new("devices.remote_logging", "Remote device log level and log upload");



    /// <summary>
    /// Every feature, in catalogue order (the comparison table's row order).
    /// </summary>
    public static IReadOnlyList<LicenseFeature> All { get; } =
    [
        PickingCore, PickingChaseMode, PickingShortsAmendments, PickingBulk,
        WorkspaceConfigure, WorkspaceSupervise, WorkspaceResolve, WorkspaceReport, WorkspaceInsights,
        ReportsBuiltIn, ReportsCustomViews,
        MessagingSupervisorPicker, MessagingPickerToPicker, NotificationsEmail, Issues,
        ConnectorsAllFormats, AuthOidc, AuthLocal, Backups, SelfUpdate,
        DevicesSingleUseEnrollment, DevicesBulkEnrollment, DevicesRemoteLogging,
    ];



    /// <summary>
    /// The feature of that name, or null.
    /// </summary>
    public static LicenseFeature? Find(string? name)
    {
        return All.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal));
    }
}
