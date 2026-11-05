// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Wolfgang.AuditTrail;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Integrity;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// One row of <c>core.user_role</c> (E10.3): a user holds a role everywhere (<see cref="SiteId"/> null) or
/// at one site, optionally until <see cref="ExpiresAt"/>. Sites are not entities yet, so the site id is a
/// plain number until they arrive.
/// </summary>
public sealed class UserRole : IVersionedEntity, ISignedEntity
{
    /// <summary>The server-assigned identifier.</summary>
    public long Id { get; set; }



    /// <summary>The user.</summary>
    public long UserId { get; set; }



    /// <summary>The role.</summary>
    public long RoleId { get; set; }



    /// <summary>The site, or null for everywhere.</summary>
    public long? SiteId { get; set; }



    /// <summary>When the assignment stops applying, or null.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }



    /// <summary>When the row was written (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }



    /// <summary>Who wrote the row.</summary>
    public string UpdatedBy { get; set; } = string.Empty;



    /// <inheritdoc/>
    public long RowVersion { get; set; }



    /// <summary>The role.</summary>
    public Role? Role { get; set; }



    /// <inheritdoc/>
    [NotAudited]
    public string? Signature { get; set; }



    /// <summary>
    /// E10.4: who holds which role where, until when.
    /// </summary>
    public string CanonicalContent()
    {
        return string.Join('\n', "user_role", UserId.ToString(CultureInfo.InvariantCulture), RoleId.ToString(CultureInfo.InvariantCulture), SiteId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, SignedContent.Timestamp(ExpiresAt));
    }
}
