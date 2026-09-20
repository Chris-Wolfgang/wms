// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Infrastructure.Database.Settings;

/// <summary>
/// <see cref="ISettings"/> over <c>core.setting</c> (E6.3, E7). Reads come from the cached snapshot: a
/// scope's own row, else the nearest ancestor's configured value, else the key's default. A write validates
/// against the registry, refuses a scope an ancestor has delegated past (E7.2), upserts the scope's row
/// (configured and effective), walks the hierarchy down rewriting the effective value of descendants that
/// inherit (a descendant with its own configured value keeps it, E7.1), saves everything in one transaction
/// and invalidates the cache.
/// </summary>
public sealed class EfSettings : ISettings
{
    private readonly WmsDbContext _context;
    private readonly SettingRegistry _registry;
    private readonly ISettingScopeHierarchy _hierarchy;
    private readonly SettingsCache _cache;
    private readonly TimeProvider _timeProvider;



    /// <summary>
    /// Creates the accessor.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfSettings(WmsDbContext context, SettingRegistry registry, ISettingScopeHierarchy hierarchy, SettingsCache cache, TimeProvider timeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _hierarchy = hierarchy ?? throw new ArgumentNullException(nameof(hierarchy));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }



    /// <inheritdoc/>
    public async Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        Require(key);
        var snapshot = await SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var (text, _) = await EffectiveAsync(key, scope, snapshot, cancellationToken).ConfigureAwait(false);
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
            throw new SettingException(SettingErrorCodes.ScopeNotAllowed, $"{key.Name} cannot be configured at the {scope.Type.StoredName()} scope.");
        }

        if (key.Validate(value) is { } reason)
        {
            throw new SettingException(SettingErrorCodes.InvalidValue, reason);
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
            throw new SettingException(SettingErrorCodes.ModeNotAllowed, $"{key.Name} cannot be delegated {mode.StoredName()} at the {scope.Type.StoredName()} scope.");
        }

        if (mode == CascadeMode.Value)
        {
            return await ResetCoreAsync(key, scope, updatedBy, cancellationToken).ConfigureAwait(false);
        }

        await RequireDecidedHereAsync(key, scope, cancellationToken).ConfigureAwait(false);
        var row = await RowAsync(scope, key.Name, cancellationToken).ConfigureAwait(false) ?? Add(scope, key.Name);
        var inherited = await InheritedFromStoreAsync(key, scope, cancellationToken).ConfigureAwait(false);
        row.ConfiguredValue = null;
        row.CascadeMode = mode.StoredName();
        Touch(row, inherited, updatedBy, _timeProvider.GetUtcNow());
        await CascadeAsync(key, scope, inherited, updatedBy, row.UpdatedAt, cancellationToken).ConfigureAwait(false);
        return await CommitAsync(key, scope, cancellationToken).ConfigureAwait(false);
    }



    /// <inheritdoc/>
    public async Task<int> PopulateAsync(SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        var created = 0;
        var now = _timeProvider.GetUtcNow();
        foreach (var key in _registry.All.Where(k => k.AllowsScope(scope.Type)))
        {
            if (await RowAsync(scope, key.Name, cancellationToken).ConfigureAwait(false) is not null)
            {
                continue;
            }

            var row = Add(scope, key.Name);
            Touch(row, await InheritedFromStoreAsync(key, scope, cancellationToken).ConfigureAwait(false), updatedBy, now);
            created++;
        }

        if (created > 0)
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _cache.Invalidate();
        }

        return created;
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
            throw new SettingException(failure.Code, failure.Detail);
        }

        return WriteAsync(Registered(name), scope, text, updatedBy, cancellationToken);
    }



    private async Task<SettingValue> WriteAsync(SettingKey key, SettingScopeRef scope, string text, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        await RequireDecidedHereAsync(key, scope, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        var row = await RowAsync(scope, key.Name, cancellationToken).ConfigureAwait(false) ?? Add(scope, key.Name);
        row.ConfiguredValue = text;
        row.CascadeMode = CascadeMode.Value.StoredName();
        Touch(row, text, updatedBy, now);
        await CascadeAsync(key, scope, text, updatedBy, now, cancellationToken).ConfigureAwait(false);
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

        var inherited = await InheritedFromStoreAsync(key, scope, cancellationToken).ConfigureAwait(false);
        row.ConfiguredValue = null;
        row.CascadeMode = CascadeMode.Value.StoredName();
        Touch(row, inherited, updatedBy, _timeProvider.GetUtcNow());
        await CascadeAsync(key, scope, inherited, updatedBy, row.UpdatedAt, cancellationToken).ConfigureAwait(false);
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
                throw new SettingException(SettingErrorCodes.DecidedElsewhere, $"{key.Name} is decided {mode.StoredName()} (set by {ancestor}); it cannot be configured at the {scope.Type.StoredName()} scope.");
            }

            parent = await _hierarchy.ParentAsync(ancestor, cancellationToken).ConfigureAwait(false);
        }
    }



    /// <summary>
    /// Rewrites the effective value of every descendant that inherits; a descendant with its own configured
    /// value keeps it and shields its subtree (E7.1, E7.2). A delegating descendant inherits for display and
    /// passes the value on to the children that have not decided.
    /// </summary>
    private async Task CascadeAsync(SettingKey key, SettingScopeRef scope, string effective, string updatedBy, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var child in await _hierarchy.ChildrenAsync(scope, cancellationToken).ConfigureAwait(false))
        {
            if (!key.AllowsScope(child.Type))
            {
                continue;
            }

            var row = await RowAsync(child, key.Name, cancellationToken).ConfigureAwait(false);
            if (row?.ConfiguredValue is not null)
            {
                continue;
            }

            if (row is not null)
            {
                Touch(row, effective, updatedBy, now);
            }

            await CascadeAsync(key, child, effective, updatedBy, now, cancellationToken).ConfigureAwait(false);
        }
    }



    private async Task<SettingValue> CommitAsync(SettingKey key, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _cache.Invalidate();
        return await ValueOfAsync(key, scope, await SnapshotAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }



    /// <summary>
    /// The value a scope inherits, read from the store (not the snapshot) so a write sees the current state.
    /// </summary>
    private async Task<string> InheritedFromStoreAsync(SettingKey key, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        var parent = await _hierarchy.ParentAsync(scope, cancellationToken).ConfigureAwait(false);
        while (parent is { } ancestor)
        {
            var row = await RowAsync(ancestor, key.Name, cancellationToken).ConfigureAwait(false);
            if (row?.ConfiguredValue is not null)
            {
                return row.EffectiveValue;
            }

            parent = await _hierarchy.ParentAsync(ancestor, cancellationToken).ConfigureAwait(false);
        }

        return key.DefaultText;
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
            return (own.EffectiveValue, null);
        }

        var parent = await _hierarchy.ParentAsync(scope, cancellationToken).ConfigureAwait(false);
        while (parent is { } ancestor)
        {
            var row = snapshot.Find(ancestor, key.Name);
            if (row?.ConfiguredValue is not null)
            {
                return (own?.EffectiveValue ?? row.EffectiveValue, ancestor.ToString());
            }

            parent = await _hierarchy.ParentAsync(ancestor, cancellationToken).ConfigureAwait(false);
        }

        return (own?.EffectiveValue ?? key.DefaultText, SettingValue.DefaultSource);
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



    private static void Touch(Setting row, string effective, string updatedBy, DateTimeOffset now)
    {
        row.EffectiveValue = effective;
        row.UpdatedBy = updatedBy;
        row.UpdatedAt = now;
        row.DeletedAt = null;
    }



    private SettingKey Registered(string? name)
    {
        return _registry.TryGet(name, out var key) ? key : throw new SettingException(SettingErrorCodes.UnknownKey, $"'{name}' is not a registered setting.");
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
