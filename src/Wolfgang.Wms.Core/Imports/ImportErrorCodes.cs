// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// The <c>imports.*</c> error codes (E16.6): the request-level ones answer the whole call; the row-level ones
/// appear in <see cref="ImportRowResult.Code"/> next to the entity's own codes (<c>zones.invalid</c>,
/// <c>locations.barcode_taken</c>, ...).
/// </summary>
public static class ImportErrorCodes
{
    /// <summary>No site has that id.</summary>
    public static ErrorCode SiteNotFound { get; } = new("imports.site_not_found", StatusCodes.Status404NotFound, "The site does not exist.", "imports-site-not-found", ErrorSeverity.Warning);

    /// <summary>The request is not what it must be: an unknown policy, an unknown format, an empty file or too many rows.</summary>
    public static ErrorCode Invalid { get; } = new("imports.invalid", StatusCodes.Status400BadRequest, "{0}", "imports-invalid", ErrorSeverity.Error);

    /// <summary>Row level: the same key appears earlier in the file.</summary>
    public static ErrorCode DuplicateInFile { get; } = new("imports.duplicate_in_file", StatusCodes.Status400BadRequest, "{0}", "imports-duplicate-in-file", ErrorSeverity.Error);

    /// <summary>Row level: the row refers to something (a zone code) that neither the file nor the site has.</summary>
    public static ErrorCode ReferenceNotFound { get; } = new("imports.reference_not_found", StatusCodes.Status400BadRequest, "{0}", "imports-reference-not-found", ErrorSeverity.Error);

    /// <summary>Row level: a delete names a key that does not exist.</summary>
    public static ErrorCode KeyNotFound { get; } = new("imports.key_not_found", StatusCodes.Status400BadRequest, "{0}", "imports-key-not-found", ErrorSeverity.Error);

    /// <summary>Row level: resolution zones are created only in the console or the API (E16.2).</summary>
    public static ErrorCode ResolutionZone { get; } = new("imports.resolution_zone", StatusCodes.Status400BadRequest, "{0}", "imports-resolution-zone", ErrorSeverity.Error);

    /// <summary>The database is not configured.</summary>
    public static ErrorCode Unavailable { get; } = new("imports.unavailable", StatusCodes.Status503ServiceUnavailable, "Imports are unavailable until the database is configured.", "imports-unavailable", ErrorSeverity.Warning);
}
