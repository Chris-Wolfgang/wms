// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wolfgang.Wms.Core.Sites;

namespace Wolfgang.Wms.Infrastructure.Sites;

/// <summary>
/// Maps <see cref="Site"/> to <c>layout.site</c> (E16.1): the normalized code is the unique natural key, the
/// lengths come from <see cref="SiteRules"/>.
/// </summary>
public sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    /// <summary>The module schema.</summary>
    public const string Schema = "layout";

    /// <summary>The table name.</summary>
    public const string Table = "site";



    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, Schema);
        builder.Property(s => s.Code).HasMaxLength(SiteRules.CodeLength).IsRequired();
        builder.Property(s => s.CodeNormalized).HasMaxLength(SiteRules.CodeLength).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(SiteRules.NameLength).IsRequired();
        builder.Property(s => s.TimeZone).HasMaxLength(SiteRules.IdLength).IsRequired();
        builder.Property(s => s.UpdatedBy).HasMaxLength(256).IsRequired();
        builder.HasIndex(s => s.CodeNormalized).IsUnique();
    }
}
