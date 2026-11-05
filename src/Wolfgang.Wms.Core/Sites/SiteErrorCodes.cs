// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Sites;

/// <summary>
/// The <c>sites.*</c> error codes (E16.1).
/// </summary>
public static class SiteErrorCodes
{
    /// <summary>No site has that id.</summary>
    public static ErrorCode NotFound { get; } = new("sites.not_found", StatusCodes.Status404NotFound, "The site does not exist.", "sites-not-found", ErrorSeverity.Warning);

    /// <summary>Another site already has that code (compared without regard to case).</summary>
    public static ErrorCode CodeTaken { get; } = new("sites.code_taken", StatusCodes.Status409Conflict, "{0}", "sites-code-taken", ErrorSeverity.Error);

    /// <summary>A field is missing, too long or not what it must be; the message names it.</summary>
    public static ErrorCode Invalid { get; } = new("sites.invalid", StatusCodes.Status400BadRequest, "{0}", "sites-invalid", ErrorSeverity.Error);

    /// <summary>The site cannot be deactivated while releases are still open against it.</summary>
    public static ErrorCode HasOpenReleases { get; } = new("sites.has_open_releases", StatusCodes.Status409Conflict, "{0}", "sites-has-open-releases", ErrorSeverity.Error);

    /// <summary>The database is not configured.</summary>
    public static ErrorCode Unavailable { get; } = new("sites.unavailable", StatusCodes.Status503ServiceUnavailable, "Sites are unavailable until the database is configured.", "sites-unavailable", ErrorSeverity.Warning);
}
