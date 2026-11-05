// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Wolfgang.Wms.Core.Logging;

/// <summary>
/// The server's log level as the console shows it (E12.4).
/// </summary>
/// <param name="Level">The permanent level (the <c>logging.level</c> setting).</param>
/// <param name="EffectiveLevel">The level in force now.</param>
/// <param name="ElevatedLevel">The elevated level while an elevation runs, else null.</param>
/// <param name="ElevatedUntil">When the elevation ends, else null.</param>
/// <param name="MaxElevationMinutes">The longest elevation the console may start.</param>
public sealed record LoggingStatus
(
    [property: JsonConverter(typeof(JsonStringEnumConverter<LogLevel>))] LogLevel Level,
    [property: JsonConverter(typeof(JsonStringEnumConverter<LogLevel>))] LogLevel EffectiveLevel,
    [property: JsonConverter(typeof(JsonStringEnumConverter<LogLevel>))] LogLevel? ElevatedLevel,
    DateTimeOffset? ElevatedUntil,
    int MaxElevationMinutes
);
