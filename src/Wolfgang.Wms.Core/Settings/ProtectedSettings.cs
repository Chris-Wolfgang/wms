// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Secrets;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// <see cref="ISettings"/> decorator that keeps secret-kind values encrypted at rest (E8.3). A secret written
/// through it is validated in the clear, protected (<c>enc:v1:</c>) and handed on as stored text; a typed
/// read reveals what the store returns. Every other key, and every DTO (which masks secrets by kind), passes
/// straight through. It sits outside <c>CachedSettings</c>, so the cache holds ciphertext and a secret is in
/// the clear only for the read that asked for it.
/// </summary>
public sealed class ProtectedSettings : ISettings
{
    private readonly ISettings _inner;
    private readonly SettingRegistry _registry;
    private readonly ISecretProtector _protector;



    /// <summary>
    /// Creates the decorator over <paramref name="inner"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public ProtectedSettings(ISettings inner, SettingRegistry registry, ISecretProtector protector)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }



    /// <inheritdoc/>
    public async Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var value = await _inner.GetAsync(key, scope, cancellationToken).ConfigureAwait(false);
        if (key.Kind != SettingKind.Secret)
        {
            return value;
        }

        var revealed = ProtectedText.Reveal(key.Codec.Format(value), _protector)!;   // a default is stored in the clear and passes through
        return key.Codec.TryParse(revealed, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"The revealed value of {key.Name} at {scope} is not a valid {key.Kind} value.");
    }



    /// <inheritdoc/>
    public Task<SettingValue> SetAsync<T>(SettingKey<T> key, SettingScopeRef scope, T value, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.Kind == SettingKind.Secret
            ? SetProtectedAsync(key, scope, key.Codec.Format(value), updatedBy, cancellationToken)
            : _inner.SetAsync(key, scope, value, updatedBy, cancellationToken);
    }



    /// <inheritdoc/>
    public Task<SettingValue> ResetAsync(SettingKey key, SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken)
    {
        return _inner.ResetAsync(key, scope, updatedBy, cancellationToken);
    }



    /// <inheritdoc/>
    public Task<SettingValue> SetModeAsync(SettingKey key, SettingScopeRef scope, CascadeMode mode, string updatedBy, CancellationToken cancellationToken)
    {
        return _inner.SetModeAsync(key, scope, mode, updatedBy, cancellationToken);
    }



    /// <inheritdoc/>
    public Task<IReadOnlyList<SettingValue>> ListAsync(SettingScopeRef scope, CancellationToken cancellationToken)
    {
        return _inner.ListAsync(scope, cancellationToken);
    }



    /// <inheritdoc/>
    public Task<SettingValue> FindAsync(string name, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        return _inner.FindAsync(name, scope, cancellationToken);
    }



    /// <inheritdoc/>
    public Task<SettingValue> SetTextAsync(string name, SettingScopeRef scope, string? text, string updatedBy, CancellationToken cancellationToken)
    {
        return text is not null && _registry.TryGet(name, out var key) && key.Kind == SettingKind.Secret
            ? SetProtectedAsync(key, scope, text, updatedBy, cancellationToken)
            : _inner.SetTextAsync(name, scope, text, updatedBy, cancellationToken);
    }



    /// <summary>
    /// Validates the clear text against the registry (registration, scope, validator), then hands the store
    /// the protected text, which the registry accepts as already validated.
    /// </summary>
    /// <exception cref="SettingException">The key, the scope or the value is refused.</exception>
    private Task<SettingValue> SetProtectedAsync(SettingKey key, SettingScopeRef scope, string text, string updatedBy, CancellationToken cancellationToken)
    {
        if (_registry.Check(key.Name, scope.Type, text) is { } failure)
        {
            throw new SettingException(failure.Code, failure.Detail, [.. failure.Arguments]);
        }

        return _inner.SetTextAsync(key.Name, scope, _protector.Protect(text), updatedBy, cancellationToken);
    }
}
