// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The <c>license</c> module's error codes (E79.3, E79.4).
/// </summary>
public static class LicenseErrorCodes
{
    /// <summary>
    /// A pasted key did not verify or is not a key of this release's schema.
    /// </summary>
    public static ErrorCode KeyRejected { get; } = new
    (
        "license.key_rejected",
        StatusCodes.Status400BadRequest,
        "The key was refused: {0}.",
        "license-key-rejected",
        ErrorSeverity.Error
    );



    /// <summary>
    /// No installed key has the id.
    /// </summary>
    public static ErrorCode KeyNotFound { get; } = new
    (
        "license.key_not_found",
        StatusCodes.Status404NotFound,
        "No installed key has the id '{0}'.",
        "license-key-not-found",
        ErrorSeverity.Error
    );



    /// <summary>
    /// A creation is blocked by a limit: beyond the allowance, after grace, or while coverage has lapsed.
    /// </summary>
    public static ErrorCode LimitReached { get; } = new
    (
        "license.limit_reached",
        StatusCodes.Status403Forbidden,
        "{0}",
        "license-limit-reached",
        ErrorSeverity.Warning
    );



    /// <summary>
    /// The feature is not in the installed tier.
    /// </summary>
    public static ErrorCode FeatureNotLicensed { get; } = new
    (
        "license.feature_not_licensed",
        StatusCodes.Status403Forbidden,
        "'{0}' is not included in the {1} tier.",
        "license-feature-not-licensed",
        ErrorSeverity.Warning
    );



    /// <summary>
    /// Every code of the module.
    /// </summary>
    public static IReadOnlyList<ErrorCode> All { get; } = [KeyRejected, KeyNotFound, LimitReached, FeatureNotLicensed];
}
