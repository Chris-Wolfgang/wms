// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// This release's tier table (E79.1, E79.2). Changing it is a release: a feature added to a tier or a limit
/// raised is fine (the CI check allows additions only); a feature that belongs in a higher tier ships as a
/// distinct feature. The free tier is compiled in here and nowhere else — not a database row, not a setting.
/// </summary>
public static class TierTables
{
    /// <summary>
    /// Every v1 feature the free tier includes, named individually (E79.2).
    /// </summary>
    public static IReadOnlyList<LicenseFeature> FreeFeatures { get; } =
    [
        LicenseFeatures.PickingCore, LicenseFeatures.PickingChaseMode, LicenseFeatures.PickingShortsAmendments,
        LicenseFeatures.WorkspaceConfigure, LicenseFeatures.WorkspaceSupervise, LicenseFeatures.WorkspaceResolve, LicenseFeatures.WorkspaceReport,
        LicenseFeatures.ReportsBuiltIn, LicenseFeatures.MessagingSupervisorPicker, LicenseFeatures.NotificationsEmail, LicenseFeatures.Issues,
        LicenseFeatures.ConnectorsAllFormats, LicenseFeatures.AuthOidc, LicenseFeatures.AuthLocal, LicenseFeatures.Backups, LicenseFeatures.SelfUpdate,
        LicenseFeatures.DevicesSingleUseEnrollment,
    ];



    /// <summary>
    /// The features paid from v1 (E79.2); tier assignment beyond free is commercial (E77).
    /// </summary>
    public static IReadOnlyList<LicenseFeature> PaidFeatures { get; } =
    [
        LicenseFeatures.WorkspaceInsights, LicenseFeatures.ReportsCustomViews, LicenseFeatures.PickingBulk,
        LicenseFeatures.MessagingPickerToPicker, LicenseFeatures.DevicesRemoteLogging, LicenseFeatures.DevicesBulkEnrollment,
    ];



    /// <summary>
    /// The release's table.
    /// </summary>
    public static TierTable Current { get; } = new(ReleaseInfo.Version,
    [
        new TierDefinition(LicenseTiers.Free, FreeFeatures, Limits(sites: 1, devices: 5, users: null, totesPerPicker: 1)),
        new TierDefinition(LicenseTiers.Pro, FreeFeatures.Concat(PaidFeatures), Limits(sites: null, devices: 5, users: null, totesPerPicker: 5)),
        new TierDefinition(LicenseTiers.Enterprise, FreeFeatures.Concat(PaidFeatures), Limits(sites: null, devices: 5, users: null, totesPerPicker: null)),
    ]);



    private static Dictionary<string, LimitValue> Limits(int? sites, int? devices, int? users, int? totesPerPicker)
    {
        return new Dictionary<string, LimitValue>(StringComparer.Ordinal)
        {
            [LicenseLimits.Sites.Name] = Value(sites),
            [LicenseLimits.Devices.Name] = Value(devices),
            [LicenseLimits.Users.Name] = Value(users),
            [LicenseLimits.MaxTotesPerPicker.Name] = Value(totesPerPicker),
        };
    }



    private static LimitValue Value(int? count)
    {
        return count is { } n ? LimitValue.Of(n) : LimitValue.Unlimited;
    }
}
