// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Logging;

/// <summary>
/// The runtime log level (E12.4): a permanent level and a timed elevation, all settings, applied within
/// seconds by <see cref="LogLevelSync"/>. Elevation only lowers the threshold (Trace, Debug or Information)
/// and reverts by itself when its end passes.
/// </summary>
public static class LogLevelSettings
{
    /// <summary>The server's minimum level.</summary>
    public static readonly SettingKey<LogLevel> Level = new("logging.level", LogLevel.Information, "The server's minimum log level.", SettingCodecs.Enum<LogLevel>())
    {
        Scopes = SettingScopes.Organization,
        Validator = v => v is >= LogLevel.Trace and <= LogLevel.Critical ? null : "must be a log level (Trace to Critical)",
    };

    /// <summary>The level while elevated.</summary>
    public static readonly SettingKey<LogLevel> ElevatedLevel = new("logging.elevated_level", LogLevel.Information, "The minimum level while the timed elevation runs (Trace, Debug or Information).", SettingCodecs.Enum<LogLevel>())
    {
        Scopes = SettingScopes.Organization,
        Validator = v => v is >= LogLevel.Trace and <= LogLevel.Information ? null : "elevation only lowers the threshold: Trace, Debug or Information",
    };

    /// <summary>When the elevation ends; in the past when there is none.</summary>
    public static readonly SettingKey<DateTimeOffset> ElevatedUntil = new("logging.elevated_until", DateTimeOffset.UnixEpoch, "When the timed elevation ends (UTC); in the past when there is none.")
    {
        Scopes = SettingScopes.Organization,
    };

    /// <summary>The longest elevation the console may start.</summary>
    public static readonly SettingKey<int> MaxElevationMinutes = new("logging.elevation_max_minutes", 120, "The longest timed elevation, in minutes.")
    {
        Scopes = SettingScopes.Organization,
        Validator = v => v is >= 1 and <= 1440 ? null : "must be between 1 and 1440 minutes",
    };



    /// <summary>
    /// Every key, for the module descriptor.
    /// </summary>
    public static IReadOnlyList<SettingKey> All { get; } = [Level, ElevatedLevel, ElevatedUntil, MaxElevationMinutes];



    /// <summary>
    /// The level in force at <paramref name="now"/>: the elevated one until its end, else the permanent one.
    /// </summary>
    public static LogLevel Effective(LogLevel level, LogLevel elevated, DateTimeOffset elevatedUntil, DateTimeOffset now)
    {
        return elevatedUntil > now ? elevated : level;
    }
}
