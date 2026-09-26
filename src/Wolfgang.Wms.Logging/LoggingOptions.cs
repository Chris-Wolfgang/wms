// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Logging;

/// <summary>
/// The bootstrap logging keys (E12.2), read once at start from <c>Wms:Logging</c>: where log lines go and
/// the level the host boots with. Everything else about logging (the runtime level, elevation) is a
/// setting, changed without a restart (E12.4).
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public const string SectionName = "Wms:Logging";



    /// <summary>
    /// The level the host boots with, until the settings take over: Verbose, Debug, Information (default),
    /// Warning, Error or Fatal.
    /// </summary>
    public string Level { get; set; } = "Information";



    /// <summary>
    /// Write plain JSON lines to standard output (the container log pipe; asynchronous, no colours). Null
    /// means "on unless a file or the Event Log is configured": a Windows service or IIS install has no
    /// console worth writing to.
    /// </summary>
    public bool? Stdout { get; set; }



    /// <summary>
    /// The rolling file (JSON lines, one file per day, <see cref="FileRetainedDays"/> kept), or null for none.
    /// The path may contain a directory; it is created. Use a volume in a container.
    /// </summary>
    public string? FilePath { get; set; }



    /// <summary>
    /// How many daily files to keep.
    /// </summary>
    public int FileRetainedDays { get; set; } = 14;



    /// <summary>
    /// The Windows Event Log source, or null for none. Ignored (with a warning) on other platforms.
    /// </summary>
    public string? EventLogSource { get; set; }



    /// <summary>
    /// The OTLP endpoint of the customer's OpenTelemetry collector (every other destination, from Seq to
    /// CloudWatch, is reached through it), or null for none.
    /// </summary>
    public string? OpenTelemetryEndpoint { get; set; }



    /// <summary>
    /// The OTLP protocol: <c>grpc</c> (default) or <c>http</c> (HTTP/protobuf).
    /// </summary>
    public string OpenTelemetryProtocol { get; set; } = "grpc";



    /// <summary>
    /// Verbose lines and SQL command logging allowed per second before the rest of that second is dropped.
    /// </summary>
    public int VerbosePerSecond { get; set; } = 200;



    /// <summary>
    /// True when standard output receives log lines: explicitly, or by default when no file and no Event Log
    /// is configured.
    /// </summary>
    public bool WritesToStdout => Stdout ?? (FilePath is null && EventLogSource is null);
}
