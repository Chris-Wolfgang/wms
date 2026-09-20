// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Devices;

/// <summary>
/// Error codes for device version enforcement (E82.7), defined once (E1.13).
/// </summary>
public static class DeviceErrorCodes
{
    /// <summary>
    /// The request carried no <c>X-Wms-Device-Version</c> header.
    /// </summary>
    public static ErrorCode VersionMissing { get; } = new
    (
        "device.version_missing",
        StatusCodes.Status400BadRequest,
        "The device did not send its app version.",
        "device-version-missing",
        ErrorSeverity.Error
    );



    /// <summary>
    /// The header value is not a version.
    /// </summary>
    public static ErrorCode VersionInvalid { get; } = new
    (
        "device.version_invalid",
        StatusCodes.Status400BadRequest,
        "'{0}' is not an app version.",
        "device-version-invalid",
        ErrorSeverity.Error
    );



    /// <summary>
    /// The app is older than the site's minimum; the device must update before it can continue.
    /// </summary>
    public static ErrorCode VersionTooOld { get; } = new
    (
        "device.version_too_old",
        StatusCodes.Status426UpgradeRequired,
        "App version {0} is below the minimum {1}; update the app.",
        "device-version-too-old",
        ErrorSeverity.Warning
    );
}
