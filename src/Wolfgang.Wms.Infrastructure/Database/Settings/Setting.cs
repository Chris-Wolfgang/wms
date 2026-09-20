// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Infrastructure.Database.Sync;

namespace Wolfgang.Wms.Infrastructure.Database.Settings;

/// <summary>
/// One row of <c>core.setting</c> (E6.2): a setting's value at one scope. <c>configured_value</c> is what an
/// administrator set here (null when the scope inherits); <c>effective_value</c> is what applies here after
/// the cascade (E7.1), always present. Row-versioned (the sync watermark and the concurrency token) and
/// soft-deleted (a reset travels to devices as a delta), so it is a synced master table.
/// </summary>
public sealed class Setting : ISyncedEntity
{
    /// <summary>
    /// Longest stored scope name.
    /// </summary>
    public const int ScopeTypeLength = 16;



    /// <summary>
    /// Longest setting name.
    /// </summary>
    public const int KeyLength = 128;



    /// <summary>
    /// Longest user identifier.
    /// </summary>
    public const int UpdatedByLength = 256;



    /// <inheritdoc/>
    public long Id { get; set; }



    /// <summary>
    /// The scope type's stored name: <c>organization</c>, <c>site</c>, <c>zone</c> or <c>sku</c>.
    /// </summary>
    public string ScopeType { get; set; } = string.Empty;



    /// <summary>
    /// The site, zone or SKU id; 0 for the organisation.
    /// </summary>
    public long ScopeId { get; set; }



    /// <summary>
    /// The registered setting name.
    /// </summary>
    public string Key { get; set; } = string.Empty;



    /// <summary>
    /// The value configured at this scope as stored text, or null when the scope inherits.
    /// </summary>
    public string? ConfiguredValue { get; set; }



    /// <summary>
    /// The value that applies at this scope as stored text.
    /// </summary>
    public string EffectiveValue { get; set; } = string.Empty;



    /// <summary>
    /// Who last wrote the row (a user id, or the service identity for a cascade).
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;



    /// <summary>
    /// When the row was last written (UTC).
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }



    /// <inheritdoc/>
    public long RowVersion { get; set; }



    /// <inheritdoc/>
    public DateTimeOffset? DeletedAt { get; set; }
}
