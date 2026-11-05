// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.UnitTests.Settings;

/// <summary>
/// A plain <see cref="ISettings"/> store for decorator tests: text per scope and key, no inheritance, every
/// read and write counted.
/// </summary>
internal sealed class InMemorySettings : ISettings
{
    private readonly SettingRegistry _registry;
    private readonly Dictionary<(SettingScopeRef Scope, string Key), string> _rows = [];



    public InMemorySettings(SettingRegistry registry)
    {
        _registry = registry;
    }



    public int Reads { get; private set; }

    public int Writes { get; private set; }

    public IReadOnlyDictionary<(SettingScopeRef Scope, string Key), string> Rows => _rows;



    public Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        Reads++;
        var text = _rows.GetValueOrDefault((scope, key.Name)) ?? key.DefaultText;
        return Task.FromResult(key.Codec.TryParse(text, out var value) ? value : throw new InvalidOperationException(text));
    }



    public Task<SettingValue> SetAsync<T>(SettingKey<T> key, SettingScopeRef scope, T value, string updatedBy, CancellationToken cancellationToken)
    {
        Writes++;
        _rows[(scope, key.Name)] = key.Codec.Format(value);
        return Task.FromResult(ValueOf(key, scope));
    }



    public Task<SettingValue> ResetAsync(SettingKey key, SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken)
    {
        Writes++;
        _rows.Remove((scope, key.Name));
        return Task.FromResult(ValueOf(key, scope));
    }



    public Task<SettingValue> SetModeAsync(SettingKey key, SettingScopeRef scope, CascadeMode mode, string updatedBy, CancellationToken cancellationToken)
    {
        Writes++;
        return Task.FromResult(ValueOf(key, scope));
    }



    public Task<IReadOnlyList<SettingValue>> ListAsync(SettingScopeRef scope, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult<IReadOnlyList<SettingValue>>(_registry.All.Select(key => ValueOf(key, scope)).ToList());
    }



    public Task<SettingValue> FindAsync(string name, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(ValueOf(_registry.TryGet(name, out var key) ? key : throw new SettingException(SettingErrorCodes.UnknownKey, name, name), scope));
    }



    public Task<SettingValue> SetTextAsync(string name, SettingScopeRef scope, string? text, string updatedBy, CancellationToken cancellationToken)
    {
        Writes++;
        var key = _registry.TryGet(name, out var registered) ? registered : throw new SettingException(SettingErrorCodes.UnknownKey, name, name);
        if (text is null)
        {
            _rows.Remove((scope, key.Name));
        }
        else
        {
            _rows[(scope, key.Name)] = text;
        }

        return Task.FromResult(ValueOf(key, scope));
    }



    private SettingValue ValueOf(SettingKey key, SettingScopeRef scope)
    {
        var text = _rows.GetValueOrDefault((scope, key.Name));
        return SettingValue.Create(key, scope, text, text ?? key.DefaultText, text is null ? SettingValue.DefaultSource : null);
    }
}
