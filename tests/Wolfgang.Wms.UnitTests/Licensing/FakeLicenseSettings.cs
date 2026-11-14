// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Licensing;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.UnitTests.Licensing;

/// <summary>
/// An in-memory settings accessor over the licensing keys: reads answer the stored text or the default,
/// writes are recorded.
/// </summary>
internal sealed class FakeLicenseSettings : ISettings
{
    private readonly DefaultSettings _defaults = new(new SettingRegistry(LicenseSettings.All));

    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    public List<string> Writes { get; } = [];

    public Exception? Failure { get; set; }

    public async Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        return Values.TryGetValue(key.Name, out var text) && key.Codec.TryParse(text, out var value) ? value : await _defaults.GetAsync(key, scope, cancellationToken);
    }

    public Task<SettingValue> FindAsync(string name, SettingScopeRef scope, CancellationToken cancellationToken) => _defaults.FindAsync(name, scope, cancellationToken);

    public Task<SettingValue> SetAsync<T>(SettingKey<T> key, SettingScopeRef scope, T value, string updatedBy, CancellationToken cancellationToken)
    {
        Values[key.Name] = key.Codec.Format(value);
        Writes.Add($"{key.Name}={Values[key.Name]} by {updatedBy}");
        return FindAsync(key.Name, scope, cancellationToken);
    }

    public Task<SettingValue> ResetAsync(SettingKey key, SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<SettingValue> SetModeAsync(SettingKey key, SettingScopeRef scope, CascadeMode mode, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<int> PopulateAsync(SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<SettingValue>> ListAsync(SettingScopeRef scope, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<SettingValue> SetTextAsync(string name, SettingScopeRef scope, string? text, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();
}
