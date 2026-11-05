// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// Builds a policy for every <c>permission:&lt;name&gt;</c> the catalog knows, on demand, so endpoints declare
/// permissions without a policy registration per name (E10.1); every other policy name goes to the default
/// provider.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    /// <summary>
    /// The prefix of a permission policy name.
    /// </summary>
    public const string Prefix = "permission:";



    private readonly DefaultAuthorizationPolicyProvider _fallback;
    private readonly PermissionCatalog _catalog;



    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options, PermissionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(options);
        _fallback = new DefaultAuthorizationPolicyProvider(options);
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }



    /// <summary>
    /// The policy name of a permission.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="permissionName"/> is null or blank.</exception>
    public static string PolicyName(string permissionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        return Prefix + permissionName;
    }



    /// <inheritdoc/>
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        ArgumentNullException.ThrowIfNull(policyName);

        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return _fallback.GetPolicyAsync(policyName);
        }

        var permission = _catalog.Find(policyName[Prefix.Length..]);
        return permission is null
            ? Task.FromResult<AuthorizationPolicy?>(null)
            : Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)).Build());
    }



    /// <inheritdoc/>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallback.GetDefaultPolicyAsync();
    }



    /// <inheritdoc/>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallback.GetFallbackPolicyAsync();
    }
}
