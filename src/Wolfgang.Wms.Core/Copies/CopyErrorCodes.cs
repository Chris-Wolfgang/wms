// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Copies;

/// <summary>
/// The <c>copies.*</c> error codes (E16.5).
/// </summary>
public static class CopyErrorCodes
{
    /// <summary>The source (site, zone, location) or the target (site, zone) does not exist.</summary>
    public static ErrorCode NotFound { get; } = new("copies.not_found", StatusCodes.Status404NotFound, "{0}", "copies-not-found", ErrorSeverity.Warning);

    /// <summary>The request is not what it must be; the message names the field.</summary>
    public static ErrorCode Invalid { get; } = new("copies.invalid", StatusCodes.Status400BadRequest, "{0}", "copies-invalid", ErrorSeverity.Error);

    /// <summary>A copy would need a code that is already in use (a site code, a zone code within the site, a bin code within the site).</summary>
    public static ErrorCode CodeTaken { get; } = new("copies.code_taken", StatusCodes.Status409Conflict, "{0}", "copies-code-taken", ErrorSeverity.Error);

    /// <summary>A copy would need a barcode that is already in use within the site.</summary>
    public static ErrorCode BarcodeTaken { get; } = new("copies.barcode_taken", StatusCodes.Status409Conflict, "{0}", "copies-barcode-taken", ErrorSeverity.Error);

    /// <summary>The database is not configured.</summary>
    public static ErrorCode Unavailable { get; } = new("copies.unavailable", StatusCodes.Status503ServiceUnavailable, "Copies are unavailable until the database is configured.", "copies-unavailable", ErrorSeverity.Warning);
}
