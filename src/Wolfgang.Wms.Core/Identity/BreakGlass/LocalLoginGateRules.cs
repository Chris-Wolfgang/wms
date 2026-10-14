// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.BreakGlass;

/// <summary>
/// The limits of an unlock window (E9.3): always timed, 30 minutes unless the operator says otherwise, never
/// open-ended (use a large value such as 1200 minutes when a long window is needed).
/// </summary>
public static class LocalLoginGateRules
{
    /// <summary>
    /// The window opened when no duration is given.
    /// </summary>
    public static TimeSpan DefaultWindow { get; } = TimeSpan.FromMinutes(30);



    /// <summary>
    /// The shortest window accepted.
    /// </summary>
    public static TimeSpan MinWindow { get; } = TimeSpan.FromMinutes(1);



    /// <summary>
    /// The longest window accepted (30 days); there is no open-ended unlock.
    /// </summary>
    public static TimeSpan MaxWindow { get; } = TimeSpan.FromDays(30);



    /// <summary>
    /// Null when <paramref name="window"/> is acceptable, else the reason.
    /// </summary>
    public static string? Validate(TimeSpan window)
    {
        return window >= MinWindow && window <= MaxWindow
            ? null
            : $"The unlock window must be between {MinWindow.TotalMinutes:0} minute and {MaxWindow.TotalDays:0} days.";
    }
}
