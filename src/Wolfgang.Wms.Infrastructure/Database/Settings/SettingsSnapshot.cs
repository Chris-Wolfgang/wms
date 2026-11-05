// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Infrastructure.Database.Settings;

/// <summary>
/// Every live row of <c>core.setting</c> at one moment (E6.3), keyed by scope and name: the read model the
/// accessor serves from between cache refreshes.
/// </summary>
public sealed class SettingsSnapshot
{
    private readonly Dictionary<(string ScopeType, long ScopeId, string Key), Setting> _rows;



    /// <summary>
    /// Builds the snapshot from detached rows.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
    public SettingsSnapshot(IEnumerable<Setting> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _rows = rows.ToDictionary(r => (r.ScopeType, r.ScopeId, r.Key));
    }



    /// <summary>
    /// Number of rows.
    /// </summary>
    public int Count => _rows.Count;



    /// <summary>
    /// The row at <paramref name="scope"/> for <paramref name="key"/>, or null when nothing is stored there.
    /// </summary>
    public Setting? Find(SettingScopeRef scope, string key)
    {
        return _rows.GetValueOrDefault((scope.Type.StoredName(), scope.Id, key));
    }
}
