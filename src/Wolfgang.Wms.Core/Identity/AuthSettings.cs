// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// The <c>auth</c> module's settings (E9.2, E10.5): organisation-wide, edited in the Configure workspace.
/// </summary>
public static class AuthSettings
{
    /// <summary>
    /// Failed local sign-ins before the account locks.
    /// </summary>
    public static readonly SettingKey<int> LockoutThreshold = new("auth.local.lockout_threshold", 5, "Failed sign-ins before a local account is locked.")
    {
        Scopes = SettingScopes.Organization,
        Validator = v => v is >= 1 and <= 100 ? null : "must be between 1 and 100",
    };



    /// <summary>
    /// How long a locked account stays locked.
    /// </summary>
    public static readonly SettingKey<TimeSpan> LockoutDuration = new("auth.local.lockout_duration", TimeSpan.FromMinutes(15), "How long a locked local account stays locked.")
    {
        Scopes = SettingScopes.Organization,
        Validator = v => v >= TimeSpan.FromMinutes(1) && v <= TimeSpan.FromDays(1) ? null : "must be between 1 minute and 1 day",
    };



    /// <summary>
    /// Absolute lifetime of a console session (E10.5): default 8 hours, at most 24.
    /// </summary>
    public static readonly SettingKey<TimeSpan> SessionLifetime = new("auth.session.lifetime", TimeSpan.FromHours(8), "How long a console session may last in total.")
    {
        Scopes = SettingScopes.Organization,
        Validator = v => v >= TimeSpan.FromMinutes(5) && v <= TimeSpan.FromHours(24) ? null : "must be between 5 minutes and 24 hours",
    };



    /// <summary>
    /// Every key, for the module descriptor.
    /// </summary>
    public static IReadOnlyList<SettingKey> All { get; } = [LockoutThreshold, LockoutDuration, SessionLifetime];
}
