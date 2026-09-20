// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.AuditTrail;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Integrity;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// One row of <c>core.role</c> (E10.2): a named set of catalog permissions. Built-in roles carry their key
/// and are read-only; their permission sets follow the catalog on every start.
/// </summary>
public sealed class Role : IVersionedEntity, ISignedEntity
{
    /// <summary>Longest role name.</summary>
    public const int NameLength = 128;

    /// <summary>Longest description.</summary>
    public const int DescriptionLength = 512;

    /// <summary>Longest built-in key.</summary>
    public const int BuiltInKeyLength = 32;



    /// <summary>The server-assigned identifier.</summary>
    public long Id { get; set; }



    /// <summary>The role name as entered.</summary>
    public string Name { get; set; } = string.Empty;



    /// <summary>The name upper-cased invariantly, unique.</summary>
    public string NameNormalized { get; set; } = string.Empty;



    /// <summary>What the role is for.</summary>
    public string Description { get; set; } = string.Empty;



    /// <summary>The built-in role's key (<c>administrator</c>), or null for a custom role.</summary>
    public string? BuiltInKey { get; set; }



    /// <summary>When the row was last written (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }



    /// <summary>Who last wrote the row.</summary>
    public string UpdatedBy { get; set; } = string.Empty;



    /// <inheritdoc/>
    public long RowVersion { get; set; }



    /// <summary>The permissions the role grants.</summary>
    public List<RolePermission> Permissions { get; } = [];



    /// <inheritdoc/>
    [NotAudited]
    public string? Signature { get; set; }



    /// <summary>
    /// E10.4: the name, the built-in key and the sorted permission names.
    /// </summary>
    public string CanonicalContent()
    {
        return string.Join('\n', "role", NameNormalized, BuiltInKey ?? string.Empty, string.Join(',', Permissions.Select(p => p.PermissionName).Order(StringComparer.Ordinal)));
    }
}
