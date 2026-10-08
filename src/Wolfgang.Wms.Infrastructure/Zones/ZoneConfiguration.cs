// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wolfgang.Wms.Core.Zones;
using Wolfgang.Wms.Infrastructure.Identity;
using Wolfgang.Wms.Infrastructure.Sites;

namespace Wolfgang.Wms.Infrastructure.Zones;

/// <summary>
/// Maps <see cref="Zone"/> and <see cref="ZoneResolver"/> to <c>layout.zone</c> and <c>layout.zone_resolver</c>
/// (E16.2): the normalized code is unique within the site, the type is stored as its lower-case name, the
/// lengths come from <see cref="ZoneRules"/>, and a resolver row names a zone and a user once.
/// </summary>
public sealed class ZoneConfiguration : IEntityTypeConfiguration<Zone>, IEntityTypeConfiguration<ZoneResolver>
{
    /// <summary>The module schema.</summary>
    public const string Schema = "layout";

    /// <summary>The zone table name.</summary>
    public const string Table = "zone";

    /// <summary>The resolver table name.</summary>
    public const string ResolverTable = "zone_resolver";

    /// <summary>Longest stored type name.</summary>
    public const int TypeLength = 16;



    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, Schema);
        builder.Property(z => z.Code).HasMaxLength(ZoneRules.CodeLength).IsRequired();
        builder.Property(z => z.CodeNormalized).HasMaxLength(ZoneRules.CodeLength).IsRequired();
        builder.Property(z => z.Name).HasMaxLength(ZoneRules.NameLength).IsRequired();
        builder.Property(z => z.Type).HasConversion(t => t.ToString().ToLowerInvariant(), s => Enum.Parse<ZoneType>(s, ignoreCase: true)).HasMaxLength(TypeLength).IsRequired();
        builder.Property(z => z.WalkOrderPrefix).HasMaxLength(ZoneRules.WalkOrderPrefixLength);
        builder.Property(z => z.RestockingBin).HasMaxLength(ZoneRules.BinLength);
        builder.Property(z => z.ReturnsContainer).HasMaxLength(ZoneRules.BinLength);
        builder.Property(z => z.UpdatedBy).HasMaxLength(256).IsRequired();
        builder.HasOne<Site>().WithMany().HasForeignKey(z => z.SiteId);
        builder.HasMany(z => z.Resolvers).WithOne().HasForeignKey(r => r.ZoneId);   // Restrict like every FK (ModelConventions); the store deletes removed resolvers explicitly
        // Serves: the code is unique within a site, compared without regard to case.
        builder.HasIndex(z => new { z.SiteId, z.CodeNormalized }).IsUnique();
    }



    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ZoneResolver> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(ResolverTable, Schema);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId);
        // Serves: one row per user per zone.
        builder.HasIndex(r => new { r.ZoneId, r.UserId }).IsUnique();
    }
}
