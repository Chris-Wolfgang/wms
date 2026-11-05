// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Organization;

/// <summary>
/// Error codes of the <c>organization</c> module (E16.0).
/// </summary>
public static class OrganizationErrorCodes
{
    /// <summary>The install has no organisation yet: the first-run wizard's first step has not run.</summary>
    public static ErrorCode NotCreated { get; } = new("organization.not_created", StatusCodes.Status404NotFound, "The organization has not been created yet; run the first-run wizard.", "organization-not-created", ErrorSeverity.Warning);

    /// <summary>There is exactly one organisation per install, and it already exists.</summary>
    public static ErrorCode AlreadyExists { get; } = new("organization.already_exists", StatusCodes.Status409Conflict, "The organization already exists; edit it instead of creating another.", "organization-already-exists", ErrorSeverity.Error);

    /// <summary>A field is missing, too long, or not a known time zone, culture or address.</summary>
    public static ErrorCode Invalid { get; } = new("organization.invalid", StatusCodes.Status400BadRequest, "{0}", "organization-invalid", ErrorSeverity.Error);

    /// <summary>The organisation is unavailable until the database is configured.</summary>
    public static ErrorCode Unavailable { get; } = new("organization.unavailable", StatusCodes.Status503ServiceUnavailable, "The organization is unavailable until the database is configured.", "organization-unavailable", ErrorSeverity.Warning);
}
