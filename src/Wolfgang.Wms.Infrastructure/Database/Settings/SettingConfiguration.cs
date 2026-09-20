// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wolfgang.Wms.Infrastructure.Database.Settings;

/// <summary>
/// Maps <see cref="Setting"/> to <c>core.setting</c> (E6.2): one row per (scope type, scope id, key), the
/// conventions supplying names, the key, <c>row_version</c> and the soft-delete filter.
/// </summary>
public sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    /// <summary>
    /// The module schema settings live in.
    /// </summary>
    public const string Schema = "core";



    /// <summary>
    /// The table name.
    /// </summary>
    public const string Table = "setting";



    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, Schema);
        builder.Property(s => s.ScopeType).HasMaxLength(Setting.ScopeTypeLength).IsRequired();
        builder.Property(s => s.Key).HasMaxLength(Setting.KeyLength).IsRequired();
        builder.Property(s => s.EffectiveValue).IsRequired();
        builder.Property(s => s.UpdatedBy).HasMaxLength(Setting.UpdatedByLength).IsRequired();
        // Serves: the accessor's lookup of one setting at one scope, and the uniqueness E6.2 requires.
        builder.HasIndex(s => new { s.ScopeType, s.ScopeId, s.Key }).IsUnique();
    }
}
