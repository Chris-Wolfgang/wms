// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Zones;

/// <summary>
/// The <c>zones.*</c> error codes (E16.2).
/// </summary>
public static class ZoneErrorCodes
{
    /// <summary>No site has that id.</summary>
    public static ErrorCode SiteNotFound { get; } = new("zones.site_not_found", StatusCodes.Status404NotFound, "The site does not exist.", "zones-site-not-found", ErrorSeverity.Warning);

    /// <summary>No zone of that site has that id.</summary>
    public static ErrorCode NotFound { get; } = new("zones.not_found", StatusCodes.Status404NotFound, "The zone does not exist.", "zones-not-found", ErrorSeverity.Warning);

    /// <summary>Another zone of the site already has that code (compared without regard to case).</summary>
    public static ErrorCode CodeTaken { get; } = new("zones.code_taken", StatusCodes.Status409Conflict, "{0}", "zones-code-taken", ErrorSeverity.Error);

    /// <summary>A field is missing, too long or not what it must be; the message names it.</summary>
    public static ErrorCode Invalid { get; } = new("zones.invalid", StatusCodes.Status400BadRequest, "{0}", "zones-invalid", ErrorSeverity.Error);

    /// <summary>The zone cannot be deactivated while zone groups are still open in it.</summary>
    public static ErrorCode HasOpenGroups { get; } = new("zones.has_open_groups", StatusCodes.Status409Conflict, "{0}", "zones-has-open-groups", ErrorSeverity.Error);

    /// <summary>The database is not configured.</summary>
    public static ErrorCode Unavailable { get; } = new("zones.unavailable", StatusCodes.Status503ServiceUnavailable, "Zones are unavailable until the database is configured.", "zones-unavailable", ErrorSeverity.Warning);
}
