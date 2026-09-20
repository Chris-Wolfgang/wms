// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// A role as the API and the console see it (E10.2).
/// </summary>
/// <param name="Id">The server-assigned identifier.</param>
/// <param name="Name">The role name, unique (case-insensitive).</param>
/// <param name="Description">What the role is for.</param>
/// <param name="BuiltIn">The built-in role key (<c>administrator</c>) when the role is one, else null; built-in roles are read-only.</param>
/// <param name="Permissions">The permission names the role grants, sorted; <c>*</c> for the administrator.</param>
/// <param name="RowVersion">The row's version (the entity tag on updates).</param>
public sealed record RoleInfo(long Id, string Name, string Description, string? BuiltIn, IReadOnlyList<string> Permissions, long RowVersion)
{
    /// <summary>
    /// The entity tag for <c>If-Match</c>.
    /// </summary>
    public string Etag => Http.EntityTag.FromRowVersion((ulong)RowVersion).Value;
}
