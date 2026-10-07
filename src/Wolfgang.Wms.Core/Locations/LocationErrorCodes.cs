// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Locations;

/// <summary>
/// The <c>locations.*</c> error codes (E17.1).
/// </summary>
public static class LocationErrorCodes
{
    /// <summary>No site has that id.</summary>
    public static ErrorCode SiteNotFound { get; } = new("locations.site_not_found", StatusCodes.Status404NotFound, "The site does not exist.", "locations-site-not-found", ErrorSeverity.Warning);

    /// <summary>No location of that site has that id.</summary>
    public static ErrorCode NotFound { get; } = new("locations.not_found", StatusCodes.Status404NotFound, "The location does not exist.", "locations-not-found", ErrorSeverity.Warning);

    /// <summary>The draft names a zone that is not a zone of the site.</summary>
    public static ErrorCode ZoneNotFound { get; } = new("locations.zone_not_found", StatusCodes.Status400BadRequest, "{0}", "locations-zone-not-found", ErrorSeverity.Error);

    /// <summary>Another location of the site already has that code (compared without regard to case).</summary>
    public static ErrorCode CodeTaken { get; } = new("locations.code_taken", StatusCodes.Status409Conflict, "{0}", "locations-code-taken", ErrorSeverity.Error);

    /// <summary>Another location of the site already has that barcode.</summary>
    public static ErrorCode BarcodeTaken { get; } = new("locations.barcode_taken", StatusCodes.Status409Conflict, "{0}", "locations-barcode-taken", ErrorSeverity.Error);

    /// <summary>A field or a paging parameter is missing, too long or not what it must be; the message names it.</summary>
    public static ErrorCode Invalid { get; } = new("locations.invalid", StatusCodes.Status400BadRequest, "{0}", "locations-invalid", ErrorSeverity.Error);

    /// <summary>The database is not configured.</summary>
    public static ErrorCode Unavailable { get; } = new("locations.unavailable", StatusCodes.Status503ServiceUnavailable, "Locations are unavailable until the database is configured.", "locations-unavailable", ErrorSeverity.Warning);
}
