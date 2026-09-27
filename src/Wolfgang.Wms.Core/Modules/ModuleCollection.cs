// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Modules;

/// <summary>
/// The modules registered with a host, in registration order. One instance per host, filled during service
/// registration and read when the host maps modules.
/// </summary>
public sealed class ModuleCollection
{
    private readonly List<ModuleDescriptor> _modules = [];



    /// <summary>
    /// Registered modules in the order they were added.
    /// </summary>
    public IReadOnlyList<ModuleDescriptor> Modules => _modules;



    /// <summary>
    /// Adds a module. A second module with the same name is a configuration error, not a merge.
    /// </summary>
    /// <exception cref="InvalidOperationException">A module with the same name is already registered.</exception>
    public void Add(ModuleDescriptor module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (_modules.Any(m => string.Equals(m.Name, module.Name, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Module '{module.Name}' is already registered.");
        }

        _modules.Add(module);
    }
}
