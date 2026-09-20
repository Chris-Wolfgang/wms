// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// The accessor before a database is configured (E6.3, bootstrap): every read answers the key's default and
/// every write is refused with <c>settings.store_unavailable</c>. <c>AddWmsDatabase</c> replaces it with the
/// stored accessor.
/// </summary>
public sealed class DefaultSettings : ISettings
{
    private readonly SettingRegistry _registry;



    /// <summary>
    /// Creates the accessor over the registry.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public DefaultSettings(SettingRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }



    /// <inheritdoc/>
    public Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        Require(key);
        return Task.FromResult(key.DefaultValue);
    }



    /// <inheritdoc/>
    public Task<SettingValue> SetAsync<T>(SettingKey<T> key, SettingScopeRef scope, T value, string updatedBy, CancellationToken cancellationToken)
    {
        Require(key);
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<SettingValue> ResetAsync(SettingKey key, SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken)
    {
        Require(key);
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<IReadOnlyList<SettingValue>> ListAsync(SettingScopeRef scope, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<SettingValue>>(_registry.All.Select(key => Default(key, scope)).ToList());
    }



    /// <inheritdoc/>
    public Task<SettingValue> FindAsync(string name, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        if (!_registry.TryGet(name, out var key))
        {
            throw new SettingException(SettingErrorCodes.UnknownKey, $"'{name}' is not a registered setting.");
        }

        return Task.FromResult(Default(key, scope));
    }



    /// <inheritdoc/>
    public Task<SettingValue> SetTextAsync(string name, SettingScopeRef scope, string? text, string updatedBy, CancellationToken cancellationToken)
    {
        if (!_registry.TryGet(name, out _))
        {
            throw new SettingException(SettingErrorCodes.UnknownKey, $"'{name}' is not a registered setting.");
        }

        throw Unavailable();
    }



    private static SettingValue Default(SettingKey key, SettingScopeRef scope)
    {
        return SettingValue.Create(key, scope, configuredValue: null, key.DefaultText, SettingValue.DefaultSource);
    }



    private static SettingException Unavailable()
    {
        return new SettingException(SettingErrorCodes.StoreUnavailable, "No database is configured; settings cannot be changed until Wms:Database is set and the schema installed.");
    }



    private void Require(SettingKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_registry.Contains(key))
        {
            throw new SettingException(SettingErrorCodes.UnknownKey, $"'{key.Name}' is not a registered setting.");
        }
    }
}
