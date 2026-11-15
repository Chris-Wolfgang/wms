// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using System.Security.Claims;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// The sites a caller may see or act on for one permission (E16.3): every site when the permission (or the
/// wildcard) is granted at the organisation, else exactly the sites it is granted at. Collection endpoints
/// without a <c>siteId</c> in the route filter their rows through it, so a supervisor never sees another
/// warehouse's data; endpoints with a <c>siteId</c> are already gated by <see cref="PermissionClaims.Allows"/>.
/// </summary>
public sealed class SiteScope
{
    private readonly HashSet<long> _siteIds;



    private SiteScope(bool unrestricted, IEnumerable<long> siteIds)
    {
        IsUnrestricted = unrestricted;
        _siteIds = unrestricted ? [] : [.. siteIds];
        SiteIds = _siteIds.Order().ToList();
    }



    /// <summary>Every site: the permission is granted at the organisation.</summary>
    public static SiteScope Everywhere { get; } = new(unrestricted: true, []);

    /// <summary>No site at all: the caller holds the permission nowhere.</summary>
    public static SiteScope Nowhere { get; } = new(unrestricted: false, []);



    /// <summary>True when every site is in scope.</summary>
    public bool IsUnrestricted { get; }

    /// <summary>The sites in scope, ascending; empty when <see cref="IsUnrestricted"/> or when there are none.</summary>
    public IReadOnlyList<long> SiteIds { get; }



    /// <summary>
    /// Exactly these sites.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="siteIds"/> is null.</exception>
    public static SiteScope Only(IEnumerable<long> siteIds)
    {
        ArgumentNullException.ThrowIfNull(siteIds);

        return new SiteScope(unrestricted: false, siteIds);
    }



    /// <summary>
    /// The scope <paramref name="principal"/> holds <paramref name="permission"/> in: everywhere for an
    /// organisation grant of the permission or the wildcard, else the sites of its site grants.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="permission"/> is null.</exception>
    public static SiteScope Of(ClaimsPrincipal? principal, Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        if (principal is null)
        {
            return Nowhere;
        }

        var grants = principal.FindAll(PermissionClaims.ClaimType).Select(c => c.Value).ToList();
        if (grants.Contains(PermissionClaims.OrganizationGrant(PermissionClaims.Wildcard), StringComparer.Ordinal)
            || grants.Contains(PermissionClaims.OrganizationGrant(permission.Name), StringComparer.Ordinal))
        {
            return Everywhere;
        }

        var sites = grants
            .Select(g => SiteOf(g, permission.Name))
            .Where(id => id is not null)
            .Select(id => id!.Value);
        return new SiteScope(unrestricted: false, sites);
    }



    /// <summary>
    /// True when <paramref name="siteId"/> is in scope.
    /// </summary>
    public bool Contains(long siteId)
    {
        return IsUnrestricted || _siteIds.Contains(siteId);
    }



    private static long? SiteOf(string grant, string permissionName)
    {
        var at = grant.IndexOf("@site:", StringComparison.Ordinal);
        if (at <= 0)
        {
            return null;
        }

        var name = grant[..at];
        if (!string.Equals(name, permissionName, StringComparison.Ordinal) && !string.Equals(name, PermissionClaims.Wildcard, StringComparison.Ordinal))
        {
            return null;
        }

        return long.TryParse(grant[(at + "@site:".Length)..], NumberStyles.None, CultureInfo.InvariantCulture, out var siteId) ? siteId : null;
    }
}
