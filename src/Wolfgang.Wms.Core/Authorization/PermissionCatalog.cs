// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// Every permission the host knows (E10.1): the union of the modules' declared permissions plus the console
/// workspace permissions, built once from the <see cref="ModuleCollection"/>. Roles are built from this list
/// and nothing else (E10.2); an endpoint can require only a permission that is in it.
/// </summary>
public sealed class PermissionCatalog
{
    private readonly Dictionary<string, (Permission Permission, string Module)> _permissions;



    /// <summary>
    /// Builds the catalog from the modules' declared permissions and the console's.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="modules"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Two modules declare the same permission name.</exception>
    public PermissionCatalog(ModuleCollection modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        _permissions = new Dictionary<string, (Permission, string)>(StringComparer.Ordinal);
        foreach (var permission in ConsolePermissions.All)
        {
            _permissions.Add(permission.Name, (permission, "console"));
        }

        foreach (var module in modules.Modules)
        {
            foreach (var permission in module.Permissions)
            {
                if (!_permissions.TryAdd(permission.Name, (permission, module.Name)))
                {
                    throw new InvalidOperationException($"Permission '{permission.Name}' is declared more than once (module '{module.Name}').");
                }
            }
        }

        All = _permissions.Values.Select(v => new PermissionDescriptor(v.Permission.Name, v.Permission.Description, v.Module)).OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
    }



    /// <summary>
    /// Every permission with its module, ordered by name.
    /// </summary>
    public IReadOnlyList<PermissionDescriptor> All { get; }



    /// <summary>
    /// The permission registered under <paramref name="name"/>, or null.
    /// </summary>
    public Permission? Find(string? name)
    {
        return name is not null && _permissions.TryGetValue(name, out var entry) ? entry.Permission : null;
    }
}
