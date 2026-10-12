// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Secrets;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Infrastructure.Database.Settings;

/// <summary>
/// <see cref="ISettings"/> over <c>core.setting</c> (E6.3, E7, ADR 0009). A row is a decision; the value at any
/// scope is resolved from the cached snapshot of those rows: the scope's own configured value, else the nearest
/// ancestor's, else the key's default. A write validates against the registry, refuses a scope an ancestor
/// has delegated past (E7.2), upserts that one row and invalidates the cache; descendants that inherit see
/// the change through resolution (a descendant with its own configured value keeps it, E7.1), and a reset
/// soft-deletes the row. Secret-kind values are stored encrypted (<c>enc:v1:</c>, E8.3) through
/// <see cref="ISecretProtector"/> and decrypted only for the typed read; every DTO masks them.
/// </summary>
public sealed class EfSettings : ISettings
{
    private readonly WmsDbContext _context;
    private readonly SettingRegistry _registry;
    private readonly ISettingScopeHierarchy _hierarchy;
    private readonly SettingsCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ISecretProtector _protector;



    /// <summary>
    /// Creates the accessor.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfSettings(WmsDbContext context, SettingRegistry registry, ISettingScopeHierarchy hierarchy, SettingsCache cache, TimeProvider timeProvider, ISecretProtector protector)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _hierarchy = hierarchy ?? throw new ArgumentNullException(nameof(hierarchy));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }



    /// <inheritdoc/>
    public async Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        Require(key);
        var snapshot = await SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var (stored, _) = await EffectiveAsync(key, scope, snapshot, cancellationToken).ConfigureAwait(false);
        var text = key.Kind == SettingKind.Secret ? ProtectedText.Reveal(stored, _protector)! : stored;   // E8.3: decrypted for the typed read only
        return key.Codec.TryParse(text, out var value)
            ? value
            : throw new InvalidOperationException($"The stored value '{text}' of {key.Name} at {scope} is not a valid {key.Kind} value.");
    }



    /// <inheritdoc/>
    public Task<SettingValue> SetAsync<T>(SettingKey<T> key, SettingScopeRef scope, T value, string updatedBy, CancellationToken cancellationToken)
    {
        Require(key);
        if (!key.AllowsScope(scope.Type))
        {
            throw new SettingException(SettingErrorCodes.ScopeNotAllowed, $"{key.Name} cannot be configured at the {scope.Type.StoredName()} scope.", key.Name, scope.Type.StoredName());
        }

        if (key.Validate(value) is { } reason)
        {
            throw new SettingException(SettingErrorCodes.InvalidValue, reason, reason);
        }

        return WriteAsync(key, scope, key.Codec.Format(value), updatedBy, cancellationToken);
    }



    /// <inheritdoc/>
    public Task<SettingValue> ResetAsync(SettingKey key, SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken)
    {
        Require(key);
        return ResetCoreAsync(key, scope, updatedBy, cancellationToken);
    }



    /// <inheritdoc/>
    public async Task<SettingValue> SetModeAsync(SettingKey key, SettingScopeRef scope, CascadeMode mode, string updatedBy, CancellationToken cancellationToken)
    {
        Require(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);
        if (!CascadeModeExtensions.AllowedAt(scope.Type, key.Scopes).Contains(mode))
        {
            throw new SettingException(SettingErrorCodes.ModeNotAllowed, $"{key.Name} cannot be delegated {mode.StoredName()} at the {scope.Type.StoredName()} scope.", key.Name, mode.StoredName(), scope.Type.StoredName());
        }

        if (mode == CascadeMode.Value)
        {
            return await ResetCoreAsync(key, scope, updatedBy, cancellationToken).ConfigureAwait(false);
        }

        await RequireDecidedHereAsync(key, scope, cancellationToken).ConfigureAwait(false);
        var row = await RowAsync(scope, key.Name, cancellationToken).ConfigureAwait(false) ?? Add(scope, key.Name);
        row.ConfiguredValue = null;
        row.CascadeMode = mode.StoredName();
        Touch(row, updatedBy, _timeProvider.GetUtcNow());
        return await CommitAsync(key, scope, cancellationToken).ConfigureAwait(false);
    }



    /// <inheritdoc/>
    public async Task<IReadOnlyList<SettingValue>> ListAsync(SettingScopeRef scope, CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var values = new List<SettingValue>(_registry.All.Count);
        foreach (var key in _registry.All)
        {
            values.Add(await ValueOfAsync(key, scope, snapshot, cancellationToken).ConfigureAwait(false));
        }

        return values;
    }



    /// <inheritdoc/>
    public async Task<SettingValue> FindAsync(string name, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        var key = Registered(name);
        var snapshot = await SnapshotAsync(cancellationToken).ConfigureAwait(false);
        return await ValueOfAsync(key, scope, snapshot, cancellationToken).ConfigureAwait(false);
    }



    /// <inheritdoc/>
    public Task<SettingValue> SetTextAsync(string name, SettingScopeRef scope, string? text, string updatedBy, CancellationToken cancellationToken)
    {
        if (text is null)
        {
            return ResetCoreAsync(Registered(name), scope, updatedBy, cancellationToken);
        }

        if (_registry.Check(name, scope.Type, text) is { } failure)
        {
            throw new SettingException(failure.Code, failure.Detail, [.. failure.Arguments]);
        }

        return WriteAsync(Registered(name), scope, text, updatedBy, cancellationToken);
    }



    private async Task<SettingValue> WriteAsync(SettingKey key, SettingScopeRef scope, string text, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        await RequireDecidedHereAsync(key, scope, cancellationToken).ConfigureAwait(false);
        if (key.Kind == SettingKind.Secret)
        {
            text = _protector.Protect(text);   // E8.3: encrypted at rest in the same text column
        }

        var row = await RowAsync(scope, key.Name, cancellationToken).ConfigureAwait(false) ?? Add(scope, key.Name);
        row.ConfiguredValue = text;
        row.CascadeMode = CascadeMode.Value.StoredName();
        Touch(row, updatedBy, _timeProvider.GetUtcNow());   // descendants see the change through resolution (ADR 0009)
        return await CommitAsync(key, scope, cancellationToken).ConfigureAwait(false);
    }



    private async Task<SettingValue> ResetCoreAsync(SettingKey key, SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        var row = await RowAsync(scope, key.Name, cancellationToken).ConfigureAwait(false);
        if (row is null || (row.ConfiguredValue is null && string.Equals(row.CascadeMode, CascadeMode.Value.StoredName(), StringComparison.Ordinal)))
        {
            return await ValueOfAsync(key, scope, await SnapshotAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        }

        // No value and no delegation is no decision, so the row goes: soft-deleted, which is how a device sees
        // it leave (E5.3); a later write at this scope revives it. Descendants resolve the inherited value.
        row.ConfiguredValue = null;
        row.CascadeMode = CascadeMode.Value.StoredName();
        Touch(row, updatedBy, _timeProvider.GetUtcNow());
        row.DeletedAt = row.UpdatedAt;
        return await CommitAsync(key, scope, cancellationToken).ConfigureAwait(false);
    }



    /// <summary>
    /// E7.2: refuses a write at a scope when an ancestor delegates the decision to a scope type strictly
    /// below it (organisation "per zone" locks the site level; zones still decide, sites inherit).
    /// </summary>
    /// <exception cref="SettingException">An ancestor delegates the decision below <paramref name="scope"/>.</exception>
    private async Task RequireDecidedHereAsync(SettingKey key, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        var parent = await _hierarchy.ParentAsync(scope, cancellationToken).ConfigureAwait(false);
        while (parent is { } ancestor)
        {
            var row = await RowAsync(ancestor, key.Name, cancellationToken).ConfigureAwait(false);
            if (row is not null && CascadeModeExtensions.TryParseMode(row.CascadeMode, out var mode) && mode.DelegatesTo() is { } target
                && target != scope.Type && CascadeModeExtensions.IsBelow(target, scope.Type))
            {
                throw new SettingException(SettingErrorCodes.DecidedElsewhere, $"{key.Name} is decided {mode.StoredName()} (set by {ancestor}); it cannot be configured at the {scope.Type.StoredName()} scope.", key.Name, mode.StoredName(), ancestor, scope.Type.StoredName());
            }

            parent = await _hierarchy.ParentAsync(ancestor, cancellationToken).ConfigureAwait(false);
        }
    }



    private async Task<SettingValue> CommitAsync(SettingKey key, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _cache.Invalidate();
        return await ValueOfAsync(key, scope, await SnapshotAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }



    /// <summary>
    /// The effective text at a scope from the snapshot and where it comes from: the scope's own row when it
    /// has one, else the nearest ancestor with a configured value, else the default.
    /// </summary>
    private async Task<(string Text, string? InheritedFrom)> EffectiveAsync(SettingKey key, SettingScopeRef scope, SettingsSnapshot snapshot, CancellationToken cancellationToken)
    {
        var own = snapshot.Find(scope, key.Name);
        if (own?.ConfiguredValue is not null)
        {
            return (own.ConfiguredValue, null);
        }

        var parent = await _hierarchy.ParentAsync(scope, cancellationToken).ConfigureAwait(false);
        while (parent is { } ancestor)
        {
            var row = snapshot.Find(ancestor, key.Name);
            if (row?.ConfiguredValue is not null)
            {
                return (row.ConfiguredValue, ancestor.ToString());
            }

            parent = await _hierarchy.ParentAsync(ancestor, cancellationToken).ConfigureAwait(false);
        }

        return (key.DefaultText, SettingValue.DefaultSource);
    }



    private async Task<SettingValue> ValueOfAsync(SettingKey key, SettingScopeRef scope, SettingsSnapshot snapshot, CancellationToken cancellationToken)
    {
        var row = snapshot.Find(scope, key.Name);
        var (text, inheritedFrom) = await EffectiveAsync(key, scope, snapshot, cancellationToken).ConfigureAwait(false);
        var mode = CascadeModeExtensions.TryParseMode(row?.CascadeMode, out var parsed) ? parsed : CascadeMode.Value;
        return SettingValue.Create(key, scope, row?.ConfiguredValue, text, inheritedFrom, mode, row?.RowVersion, row?.UpdatedBy, row?.UpdatedAt);
    }



    private Task<SettingsSnapshot> SnapshotAsync(CancellationToken cancellationToken)
    {
        return _cache.GetAsync(async ct => new SettingsSnapshot(await _context.Settings.AsNoTracking().ToListAsync(ct).ConfigureAwait(false)), cancellationToken);
    }



    private Task<Setting?> RowAsync(SettingScopeRef scope, string key, CancellationToken cancellationToken)
    {
        var scopeType = scope.Type.StoredName();
        return _context.Settings
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(s => s.ScopeType == scopeType && s.ScopeId == scope.Id && s.Key == key, cancellationToken);
    }



    private Setting Add(SettingScopeRef scope, string key)
    {
        var row = new Setting { ScopeType = scope.Type.StoredName(), ScopeId = scope.Id, Key = key };
        _context.Settings.Add(row);
        return row;
    }



    private static void Touch(Setting row, string updatedBy, DateTimeOffset now)
    {
        row.UpdatedBy = updatedBy;
        row.UpdatedAt = now;
        row.DeletedAt = null;
    }



    private SettingKey Registered(string? name)
    {
        return _registry.TryGet(name, out var key) ? key : throw new SettingException(SettingErrorCodes.UnknownKey, $"'{name}' is not a registered setting.", name ?? string.Empty);
    }



    private void Require(SettingKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_registry.Contains(key))
        {
            throw new SettingException(SettingErrorCodes.UnknownKey, $"'{key.Name}' is not a registered setting.", key.Name);
        }
    }
}
