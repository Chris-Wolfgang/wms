// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Wolfgang.AuditTrail;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Integrity;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// One row of <c>core.group_role_mapping</c> (E11.2): members of a provider's group hold a role everywhere
/// or at one site. Audited, versioned and signed (a mapping decides access).
/// </summary>
public sealed class GroupRoleMapping : IVersionedEntity, ISignedEntity
{
    /// <summary>Longest provider name.</summary>
    public const int ProviderLength = 32;

    /// <summary>Longest group identifier.</summary>
    public const int GroupLength = 256;



    /// <summary>The server-assigned identifier.</summary>
    public long Id { get; set; }



    /// <summary>The provider whose group this is.</summary>
    public string Provider { get; set; } = string.Empty;



    /// <summary>The group identifier as the provider sends it.</summary>
    public string GroupKey { get; set; } = string.Empty;



    /// <summary>The role granted.</summary>
    public long RoleId { get; set; }



    /// <summary>The site, or null for everywhere.</summary>
    public long? SiteId { get; set; }



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
    /// E10.4: which group grants which role where.
    /// </summary>
    public string CanonicalContent()
    {
        return string.Join('\n', "group_role_mapping", Provider, GroupKey, RoleId.ToString(CultureInfo.InvariantCulture), SiteId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
    }
}
