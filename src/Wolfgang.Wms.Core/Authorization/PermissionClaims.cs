// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using System.Security.Claims;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// How a session carries what its user may do (E10.1, E10.3): one <c>wms:permission</c> claim per grant,
/// valued <c>name@organization</c> (everywhere) or <c>name@site:&lt;id&gt;</c> (one site), with <c>*</c>
/// standing for every permission. Holding a permission at one site never grants it at another; the only
/// way to hold it everywhere is an organisation grant.
/// </summary>
public static class PermissionClaims
{
    /// <summary>
    /// The claim type.
    /// </summary>
    public const string ClaimType = "wms:permission";



    /// <summary>
    /// The name that stands for every permission.
    /// </summary>
    public const string Wildcard = "*";



    /// <summary>
    /// The scope suffix of an organisation grant.
    /// </summary>
    public const string OrganizationScope = "organization";



    /// <summary>
    /// A grant valid everywhere.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="permissionName"/> is null or blank.</exception>
    public static string OrganizationGrant(string permissionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        return permissionName + "@" + OrganizationScope;
    }



    /// <summary>
    /// A grant valid at one site.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="permissionName"/> is null or blank.</exception>
    public static string SiteGrant(string permissionName, long siteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        return permissionName + "@site:" + siteId.ToString(CultureInfo.InvariantCulture);
    }



    /// <summary>
    /// True when <paramref name="principal"/> holds <paramref name="permission"/> everywhere, or at
    /// <paramref name="siteId"/> when one is given.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="permission"/> is null.</exception>
    public static bool Allows(ClaimsPrincipal? principal, Permission permission, long? siteId)
    {
        ArgumentNullException.ThrowIfNull(permission);
        if (principal is null)
        {
            return false;
        }

        var everywhere = new[] { OrganizationGrant(Wildcard), OrganizationGrant(permission.Name) };
        var here = siteId is { } site ? new[] { SiteGrant(Wildcard, site), SiteGrant(permission.Name, site) } : [];
        return principal.FindAll(ClaimType).Any(c => everywhere.Contains(c.Value, StringComparer.Ordinal) || here.Contains(c.Value, StringComparer.Ordinal));
    }



    /// <summary>
    /// Every grant a principal carries, sorted.
    /// </summary>
    public static IReadOnlyList<string> GrantsOf(ClaimsPrincipal? principal)
    {
        return principal is null ? [] : principal.FindAll(ClaimType).Select(c => c.Value).Order(StringComparer.Ordinal).ToList();
    }
}
