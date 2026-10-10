// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// The settings module's permissions (E10.1).
/// </summary>
public static class SettingsPermissions
{
    /// <summary>
    /// See the registry and the values at a scope.
    /// </summary>
    public static readonly Permission Read = new("settings.read", "View settings and their values");



    /// <summary>
    /// Change or reset a value, or delegate a decision.
    /// </summary>
    public static readonly Permission Write = new("settings.write", "Change settings");



    /// <summary>
    /// Every permission of the module.
    /// </summary>
    public static IReadOnlyList<Permission> All { get; } = [Read, Write];
}
