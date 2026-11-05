// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Infrastructure.Database.Settings;

/// <summary>
/// <see cref="ISettings"/> over <c>core.setting</c> (E6.3, E7, ADR 0009). A row is a decision; the value at any
/// scope is resolved from the rows on the scope's ancestor chain, read in one query: the scope's own configured
/// value, else the nearest ancestor's, else the key's default. A write validates against the registry, refuses
/// a scope an ancestor has delegated past (E7.2) and upserts that one row; descendants that inherit see the
/// change through resolution (a descendant with its own configured value keeps it, E7.1), and a reset
/// soft-deletes the row. The store is plain text in, plain text out: <see cref="CachedSettings"/> memoises
/// what it resolves and <see cref="ProtectedSettings"/> keeps secrets encrypted at rest (E8.3), both
/// decorating it in the container.
/// </summary>
public sealed class EfSettings : ISettings
{
    private readonly WmsDbContext _context;
    private readonly SettingRegistry _registry;
    private readonly ISettingScopeHierarchy _hierarchy;
    private readonly TimeProvider _timeProvider;



    /// <summary>
    /// Creates the store.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfSettings(WmsDbContext context, SettingRegistry registry, ISettingScopeHierarchy hierarchy, TimeProvider timeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _hierarchy = hierarchy ?? throw new ArgumentNullException(nameof(hierarchy));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }



    /// <inheritdoc/>
    public async Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        Require(key);
        var chain = await ChainAsync(scope, cancellationToken).ConfigureAwait(false);
        var snapshot = await SnapshotAsync(chain, key.Name, cancellationToken).ConfigureAwait(false);
        var (text, _) = Effective(key, chain, snapshot);
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

        var chain = await ChainAsync(scope, cancellationToken).ConfigureAwait(false);
        await RequireDecidedHereAsync(key, chain, cancellationToken).ConfigureAwait(false);
        var row = await RowAsync(scope, key.Name, cancellationToken).ConfigureAwait(false) ?? Add(scope, key.Name);
        row.Value = null;
        row.CascadeMode = mode.StoredName();
        Touch(row, updatedBy, _timeProvider.GetUtcNow());
        return await CommitAsync(key, chain, cancellationToken).ConfigureAwait(false);
    }



    /// <inheritdoc/>
    public async Task<IReadOnlyList<SettingValue>> ListAsync(SettingScopeRef scope, CancellationToken cancellationToken)
    {
        var chain = await ChainAsync(scope, cancellationToken).ConfigureAwait(false);
        var snapshot = await SnapshotAsync(chain, key: null, cancellationToken).ConfigureAwait(false);
        var values = new List<SettingValue>(_registry.All.Count);
        foreach (var key in _registry.All)
        {
            values.Add(ValueOf(key, chain, snapshot));
        }

        return values;
    }



    /// <inheritdoc/>
    public async Task<SettingValue> FindAsync(string name, SettingScopeRef scope, CancellationToken cancellationToken)
    {
        var key = Registered(name);
        var chain = await ChainAsync(scope, cancellationToken).ConfigureAwait(false);
        var snapshot = await SnapshotAsync(chain, key.Name, cancellationToken).ConfigureAwait(false);
        return ValueOf(key, chain, snapshot);
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

        var chain = await ChainAsync(scope, cancellationToken).ConfigureAwait(false);
        await RequireDecidedHereAsync(key, chain, cancellationToken).ConfigureAwait(false);
        var row = await RowAsync(scope, key.Name, cancellationToken).ConfigureAwait(false) ?? Add(scope, key.Name);
        row.Value = text;
        row.CascadeMode = CascadeMode.Value.StoredName();
        Touch(row, updatedBy, _timeProvider.GetUtcNow());   // descendants see the change through resolution (ADR 0009)
        return await CommitAsync(key, chain, cancellationToken).ConfigureAwait(false);
    }



    private async Task<SettingValue> ResetCoreAsync(SettingKey key, SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        var chain = await ChainAsync(scope, cancellationToken).ConfigureAwait(false);
        var row = await RowAsync(scope, key.Name, cancellationToken).ConfigureAwait(false);
        if (row is null || (row.Value is null && string.Equals(row.CascadeMode, CascadeMode.Value.StoredName(), StringComparison.Ordinal)))
        {
            return ValueOf(key, chain, await SnapshotAsync(chain, key.Name, cancellationToken).ConfigureAwait(false));
        }

        // No value and no delegation is no decision, so the row goes: soft-deleted, which is how a device sees
        // it leave (E5.3); a later write at this scope revives it. Descendants resolve the inherited value.
        row.Value = null;
        row.CascadeMode = CascadeMode.Value.StoredName();
        Touch(row, updatedBy, _timeProvider.GetUtcNow());
        row.DeletedAt = row.UpdatedAt;
        return await CommitAsync(key, chain, cancellationToken).ConfigureAwait(false);
    }



    /// <summary>
    /// E7.2: refuses a write at the scope heading <paramref name="chain"/> when an ancestor delegates the
    /// decision to a scope type strictly below it (organisation "per zone" locks the site level; zones still
    /// decide, sites inherit).
    /// </summary>
    /// <exception cref="SettingException">An ancestor delegates the decision below the scope.</exception>
    private async Task RequireDecidedHereAsync(SettingKey key, IReadOnlyList<SettingScopeRef> chain, CancellationToken cancellationToken)
    {
        var scope = chain[0];
        foreach (var ancestor in chain.Skip(1))
        {
            var row = await RowAsync(ancestor, key.Name, cancellationToken).ConfigureAwait(false);
            if (row is not null && CascadeModeExtensions.TryParseMode(row.CascadeMode, out var mode) && mode.DelegatesTo() is { } target
                && target != scope.Type && CascadeModeExtensions.IsBelow(target, scope.Type))
            {
                throw new SettingException(SettingErrorCodes.DecidedElsewhere, $"{key.Name} is decided {mode.StoredName()} (set by {ancestor}); it cannot be configured at the {scope.Type.StoredName()} scope.", key.Name, mode.StoredName(), ancestor, scope.Type.StoredName());
            }
        }
    }



    private async Task<SettingValue> CommitAsync(SettingKey key, IReadOnlyList<SettingScopeRef> chain, CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ValueOf(key, chain, await SnapshotAsync(chain, key.Name, cancellationToken).ConfigureAwait(false));
    }



    /// <summary>
    /// The effective text at the scope heading <paramref name="chain"/> and where it comes from: the scope's
    /// own row when it has a value, else the nearest ancestor with one, else the default.
    /// </summary>
    private static (string Text, string? InheritedFrom) Effective(SettingKey key, IReadOnlyList<SettingScopeRef> chain, SettingsSnapshot snapshot)
    {
        for (var i = 0; i < chain.Count; i++)
        {
            var row = snapshot.Find(chain[i], key.Name);
            if (row?.Value is not null)
            {
                return (row.Value, i == 0 ? null : chain[i].ToString());
            }
        }

        return (key.DefaultText, SettingValue.DefaultSource);
    }



    private static SettingValue ValueOf(SettingKey key, IReadOnlyList<SettingScopeRef> chain, SettingsSnapshot snapshot)
    {
        var scope = chain[0];
        var row = snapshot.Find(scope, key.Name);
        var (text, inheritedFrom) = Effective(key, chain, snapshot);
        var mode = CascadeModeExtensions.TryParseMode(row?.CascadeMode, out var parsed) ? parsed : CascadeMode.Value;
        return SettingValue.Create(key, scope, row?.Value, text, inheritedFrom, mode, row?.RowVersion, row?.UpdatedBy, row?.UpdatedAt);
    }



    /// <summary>
    /// The scope and its ancestors, nearest first, as the hierarchy answers them.
    /// </summary>
    private async Task<IReadOnlyList<SettingScopeRef>> ChainAsync(SettingScopeRef scope, CancellationToken cancellationToken)
    {
        var chain = new List<SettingScopeRef>(3) { scope };
        var parent = await _hierarchy.ParentAsync(scope, cancellationToken).ConfigureAwait(false);
        while (parent is { } ancestor)
        {
            chain.Add(ancestor);
            parent = await _hierarchy.ParentAsync(ancestor, cancellationToken).ConfigureAwait(false);
        }

        return chain;
    }



    /// <summary>
    /// The decision rows on <paramref name="chain"/> in one query, for one key or for all of them. The scope
    /// types and ids are matched as two sets, which the index serves and which can over-fetch only rows of a
    /// sibling scope with a coinciding id; the snapshot's exact lookup ignores those.
    /// </summary>
    private async Task<SettingsSnapshot> SnapshotAsync(IReadOnlyList<SettingScopeRef> chain, string? key, CancellationToken cancellationToken)
    {
        var types = chain.Select(s => s.Type.StoredName()).Distinct().ToList();
        var ids = chain.Select(s => s.Id).Distinct().ToList();
        var query = _context.Settings.AsNoTracking().Where(s => types.Contains(s.ScopeType) && ids.Contains(s.ScopeId));
        if (key is not null)
        {
            query = query.Where(s => s.Key == key);
        }

        return new SettingsSnapshot(await query.ToListAsync(cancellationToken).ConfigureAwait(false));
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
