// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Logging;

/// <summary>
/// The <c>logging</c> module's error codes (E12.4).
/// </summary>
public static class LoggingErrorCodes
{
    /// <summary>
    /// A timed elevation was refused: not a lowering level, or too long.
    /// </summary>
    public static ErrorCode ElevationRejected { get; } = new
    (
        "logging.elevation_rejected",
        StatusCodes.Status400BadRequest,
        "The elevation was refused: {0}.",
        "logging-elevation-rejected",
        ErrorSeverity.Error
    );
}
