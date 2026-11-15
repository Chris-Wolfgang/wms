// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// The one way to read and change a setting (E6.3). <see cref="GetAsync{T}"/> returns the effective value
/// at a scope (its own configured value, else the nearest ancestor's, else the key's default), served from
/// a per-instance cache invalidated by <c>row_version</c>. Every write validates against the registry
/// (registration, allowed scope, kind, validator), stores the invariant text, recomputes descendants that
/// inherit (E7.1), is audited (E6.4) and invalidates the cache. The console and the API call this; nothing
/// writes <c>core.setting</c> directly.
/// </summary>
public interface ISettings
{
    /// <summary>
    /// The effective value of <paramref name="key"/> at <paramref name="scope"/>.
    /// </summary>
    /// <exception cref="SettingException">The key is not registered.</exception>
    Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken);



    /// <summary>
    /// Configures <paramref name="value"/> at <paramref name="scope"/> and cascades it to descendants that
    /// inherit.
    /// </summary>
    /// <exception cref="SettingException">The key is not registered, the scope is not allowed, or the validator rejects the value.</exception>
    Task<SettingValue> SetAsync<T>(SettingKey<T> key, SettingScopeRef scope, T value, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Removes the decision at <paramref name="scope"/>, so it and every descendant that inherits resolve the
    /// nearest ancestor's value again (ADR 0009).
    /// </summary>
    /// <exception cref="SettingException">The key is not registered.</exception>
    Task<SettingValue> ResetAsync(SettingKey key, SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Delegates the decision at <paramref name="scope"/> to the scopes <paramref name="mode"/> names (E7.2):
    /// the scope drops its own value and contributes nothing; <see cref="CascadeMode.Value"/> restores the
    /// ordinary inheriting scope.
    /// </summary>
    /// <exception cref="SettingException">The key is not registered or the mode is not allowed at the scope.</exception>
    Task<SettingValue> SetModeAsync(SettingKey key, SettingScopeRef scope, CascadeMode mode, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Every registered setting at <paramref name="scope"/> with its configured and effective text.
    /// </summary>
    Task<IReadOnlyList<SettingValue>> ListAsync(SettingScopeRef scope, CancellationToken cancellationToken);



    /// <summary>
    /// One setting at <paramref name="scope"/> by name (the API's and the console's read).
    /// </summary>
    /// <exception cref="SettingException">No setting is registered under <paramref name="name"/>.</exception>
    Task<SettingValue> FindAsync(string name, SettingScopeRef scope, CancellationToken cancellationToken);



    /// <summary>
    /// Configures stored text by name (the API's and the console's write); null text resets.
    /// </summary>
    /// <exception cref="SettingException">The name is not registered, the scope is not allowed, or the text is invalid.</exception>
    Task<SettingValue> SetTextAsync(string name, SettingScopeRef scope, string? text, string updatedBy, CancellationToken cancellationToken);
}
