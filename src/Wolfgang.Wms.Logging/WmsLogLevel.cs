// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace Wolfgang.Wms.Logging;

/// <summary>
/// The one level switch of the host (E12.4): the logger's minimum level is controlled by it, the boot value
/// comes from configuration, and the settings-driven sync moves it at runtime. EF Core has its own switch that
/// follows it: Warning normally, the host's level once that is Debug or more verbose, so an elevation also brings
/// the SQL diagnostics (still under the verbose rate limit).
/// </summary>
public sealed class WmsLogLevel
{
    /// <summary>
    /// Creates the switch at <paramref name="initial"/>.
    /// </summary>
    public WmsLogLevel(LogEventLevel initial = LogEventLevel.Information)
    {
        Switch = new LoggingLevelSwitch(initial);
        EntityFrameworkSwitch = new LoggingLevelSwitch(ForEntityFramework(initial));
    }



    /// <summary>
    /// The switch Serilog reads on every event.
    /// </summary>
    public LoggingLevelSwitch Switch { get; }



    /// <summary>
    /// The switch for the <c>Microsoft.EntityFrameworkCore</c> categories (see <see cref="ForEntityFramework"/>).
    /// </summary>
    public LoggingLevelSwitch EntityFrameworkSwitch { get; }



    /// <summary>
    /// The current minimum level.
    /// </summary>
    public LogEventLevel Current => Switch.MinimumLevel;



    /// <summary>
    /// Moves the minimum level; true when it changed.
    /// </summary>
    public bool Apply(LogEventLevel level)
    {
        if (Switch.MinimumLevel == level)
        {
            return false;
        }

        Switch.MinimumLevel = level;
        EntityFrameworkSwitch.MinimumLevel = ForEntityFramework(level);
        return true;
    }



    /// <summary>
    /// The EF Core level for a host level: <see cref="LogEventLevel.Warning"/> unless the host is at
    /// <see cref="LogEventLevel.Debug"/> or more verbose, then the same level.
    /// </summary>
    public static LogEventLevel ForEntityFramework(LogEventLevel level)
    {
        return level <= LogEventLevel.Debug ? level : LogEventLevel.Warning;   // SQL text is Verbose territory: only when asked for
    }



    /// <summary>
    /// The Serilog level for a Microsoft logging level.
    /// </summary>
    public static LogEventLevel ToSerilog(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            _ => LogEventLevel.Fatal,
        };
    }



    /// <summary>
    /// The Serilog level named in configuration (Serilog or Microsoft names, case-insensitive), or
    /// Information when the name is unknown.
    /// </summary>
    public static LogEventLevel Parse(string? name)
    {
        if (Enum.TryParse<LogEventLevel>(name, ignoreCase: true, out var serilog) && Enum.IsDefined(serilog))
        {
            return serilog;
        }

        return Enum.TryParse<LogLevel>(name, ignoreCase: true, out var microsoft) && Enum.IsDefined(microsoft) ? ToSerilog(microsoft) : LogEventLevel.Information;
    }
}
