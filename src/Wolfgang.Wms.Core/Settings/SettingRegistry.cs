// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// Every setting the host knows (E6.1): the union of the keys its modules declare through
/// <see cref="ModuleDescriptor.WithSettings"/>, built once from the <see cref="ModuleCollection"/>. The
/// settings pages, the generated documentation and the accessor all read from here, so a key that is not
/// registered cannot be shown, documented or written.
/// </summary>
public sealed class SettingRegistry
{
    private readonly Dictionary<string, SettingKey> _keys;



    /// <summary>
    /// Builds the registry from the modules' declared settings.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="modules"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Two modules declare the same setting name.</exception>
    public SettingRegistry(ModuleCollection modules)
        : this(Collect(modules))
    {
    }



    /// <summary>
    /// Builds the registry from explicit keys (tests, tools).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="keys"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Two keys share a name.</exception>
    public SettingRegistry(IEnumerable<SettingKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        _keys = new Dictionary<string, SettingKey>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (key is null)
            {
                throw new ArgumentException("Keys must not contain null.", nameof(keys));
            }

            if (!_keys.TryAdd(key.Name, key))
            {
                throw new InvalidOperationException($"Setting '{key.Name}' is declared more than once.");
            }
        }

        All = _keys.Values.OrderBy(k => k.Name, StringComparer.Ordinal).ToList();
    }



    /// <summary>
    /// Every registered key, ordered by name.
    /// </summary>
    public IReadOnlyList<SettingKey> All { get; }



    /// <summary>
    /// The key registered under <paramref name="name"/>, if any.
    /// </summary>
    public bool TryGet(string? name, [NotNullWhen(true)] out SettingKey? key)
    {
        if (name is null)
        {
            key = null;
            return false;
        }

        return _keys.TryGetValue(name, out key);
    }



    /// <summary>
    /// True when <paramref name="key"/> is the registered instance for its name: writes through an
    /// unregistered key are rejected even when the name looks right.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public bool Contains(SettingKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _keys.TryGetValue(key.Name, out var registered) && ReferenceEquals(registered, key);
    }



    /// <summary>
    /// Checks a write (E6.1, E6.3): the key must be registered, allow <paramref name="scope"/>, and accept
    /// <paramref name="text"/>. Returns the error code and a detail for the problem response, or null when
    /// the write is valid.
    /// </summary>
    public (ErrorCode Code, string Detail)? Check(string? name, SettingScope scope, string? text)
    {
        if (!TryGet(name, out var key))
        {
            return (SettingErrorCodes.UnknownKey, $"'{name}' is not a registered setting.");
        }

        if (!key.AllowsScope(scope))
        {
            return (SettingErrorCodes.ScopeNotAllowed, $"{key.Name} cannot be configured at the {scope.StoredName()} scope.");
        }

        var reason = key.Validate(text);
        return reason is null ? null : (SettingErrorCodes.InvalidValue, reason);
    }



    private static IEnumerable<SettingKey> Collect(ModuleCollection modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        return modules.Modules.SelectMany(m => m.Settings);
    }
}
