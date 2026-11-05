// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Infrastructure.Database.Settings;

/// <summary>
/// <see cref="ISettings"/> decorator that memoises what the store resolves (E6.3, ADR 0003): a read is a
/// dictionary hit once the current <see cref="SettingsMemo"/> generation holds it, else the store answers and
/// the generation keeps the answer. A write passes through and invalidates, so the next read opens a new
/// generation; writes by other instances reach this one through the row-version probe
/// (<see cref="SettingsCache.PollInterval"/>).
/// </summary>
public sealed class CachedSettings : ISettings
{
    private readonly ISettings _inner;
    private readonly SettingsCache _cache;



    /// <summary>
    /// Creates the decorator over <paramref name="inner"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public CachedSettings(ISettings inner, SettingsCache cache)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }



    /// <inheritdoc/>
    public async Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var memo = await _cache.GetAsync(cancellationToken).ConfigureAwait(false);
        if (memo.Typed.TryGetValue((scope, key.Name), out var cached))
        {
            return (T)cached;
        }

        var value = await _inner.GetAsync(key, scope, cancellationToken).ConfigureAwait(false);
        memo.Typed[(scope, key.Name)] = value!;
        return value;
    }



    /// <inheritdoc/>
    public async Task<SettingValue> SetAsync<T>(SettingKey<T> key, SettingScopeRef scope, T value, string updatedBy, CancellationToken cancellationToken)
    {
        var result = await _inner.SetAsync(key, scope, value, updatedBy, cancellationToken).ConfigureAwait(false);
        _cache.Invalidate();
        return result;
    }



    /// <inheritdoc/>
    public async Task<SettingValue> ResetAsync(SettingKey key, SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken)
    {
        var result = await _inner.ResetAsync(key, scope, updatedBy, cancellationToken).ConfigureAwait(false);
        _cache.Invalidate();
        return result;
    }



    /// <inheritdoc/>
    public async Task<SettingValue> SetModeAsync(SettingKey key, SettingScopeRef scope, CascadeMode mode, string updatedBy, CancellationToken cancellationToken)
    {
        var result = await _inner.SetModeAsync(key, scope, mode, updatedBy, cancellationToken).ConfigureAwait(false);
        _cache.Invalidate();
        return result;
    }



    /// <inheritdoc/>
    public async Task<IReadOnlyList<SettingValue>> ListAsync(SettingScopeRef scope, CancellationToken cancellationToken)
    {
        var memo = await _cache.GetAsync(cancellationToken).ConfigureAwait(false);
        if (memo.Lists.TryGetValue(scope, out var cached))
        {
            return cached;
        }

        var values = await _inner.ListAsync(scope, cancellationToken).ConfigureAwait(false);
        memo.Lists[scope] = values;
        return values;
    }



    /// <inheritdoc/>
    public async Task<SettingValue> FindAsync(string name, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        var memo = await _cache.GetAsync(cancellationToken).ConfigureAwait(false);
        if (memo.Values.TryGetValue((scope, name), out var cached))
        {
            return cached;
        }

        var value = await _inner.FindAsync(name, scope, cancellationToken).ConfigureAwait(false);
        memo.Values[(scope, name)] = value;
        return value;
    }



    /// <inheritdoc/>
    public async Task<SettingValue> SetTextAsync(string name, SettingScopeRef scope, string? text, string updatedBy, CancellationToken cancellationToken)
    {
        var result = await _inner.SetTextAsync(name, scope, text, updatedBy, cancellationToken).ConfigureAwait(false);
        _cache.Invalidate();
        return result;
    }
}
