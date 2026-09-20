// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.RegularExpressions;

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// Every provider registered in the host (E11.0), by name. Adding a provider kind is a release (a project
/// and a registration); enabling one is a setting.
/// </summary>
public sealed partial class AuthProviderCatalog
{
    private readonly Dictionary<string, IAuthProvider> _providers = new(StringComparer.Ordinal);



    /// <summary>
    /// Creates the catalog.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="providers"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A name is malformed or registered twice.</exception>
    public AuthProviderCatalog(IEnumerable<IAuthProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        foreach (var provider in providers)
        {
            if (!IsName(provider.Name))
            {
                throw new InvalidOperationException($"Auth provider name '{provider.Name}' must be lower-case letters, digits and dashes.");
            }

            if (!_providers.TryAdd(provider.Name, provider))
            {
                throw new InvalidOperationException($"Auth provider '{provider.Name}' is registered twice.");
            }
        }

        All = _providers.Values.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
    }



    /// <summary>
    /// Every registered provider, by name.
    /// </summary>
    public IReadOnlyList<IAuthProvider> All { get; }



    /// <summary>
    /// The provider of that name, or null.
    /// </summary>
    public IAuthProvider? Find(string? name)
    {
        return name is not null && _providers.TryGetValue(name, out var provider) ? provider : null;
    }



    /// <summary>
    /// True for a well-formed provider name.
    /// </summary>
    public static bool IsName(string? name)
    {
        return name is not null && NamePattern().IsMatch(name);
    }



    [GeneratedRegex("^[a-z][a-z0-9-]{0,31}$")]
    private static partial Regex NamePattern();
}
