// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Infrastructure.Sites;
using Wolfgang.Wms.Infrastructure.Zones;

namespace Wolfgang.Wms.Infrastructure.Locations;

/// <summary>
/// Maps <see cref="Location"/> to <c>layout.location</c> (E17.1): the normalized code and the barcode are each
/// unique within the site, the lengths come from <see cref="LocationRules"/>, and every sortable field has an
/// index ending with the id so a keyset page reads one index.
/// </summary>
public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    /// <summary>The module schema.</summary>
    public const string Schema = "layout";

    /// <summary>The table name.</summary>
    public const string Table = "location";



    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, Schema);
        builder.Property(l => l.Code).HasMaxLength(LocationRules.CodeLength).IsRequired();
        builder.Property(l => l.CodeNormalized).HasMaxLength(LocationRules.CodeLength).IsRequired();
        builder.Property(l => l.Barcode).HasMaxLength(LocationRules.BarcodeLength).IsRequired();
        builder.Property(l => l.WalkSequence).HasMaxLength(LocationRules.WalkSequenceLength).IsRequired();
        builder.Property(l => l.UpdatedBy).HasMaxLength(256).IsRequired();
        builder.HasOne<Site>().WithMany().HasForeignKey(l => l.SiteId);
        builder.HasOne<Zone>().WithMany().HasForeignKey(l => l.ZoneId);
        // Serves: the code is unique within a site, compared without regard to case; sort=code pages read it too.
        builder.HasIndex(l => new { l.SiteId, l.CodeNormalized }).IsUnique();
        // Serves: the barcode is unique within a site; a scan resolves the bin; sort=barcode pages read it.
        builder.HasIndex(l => new { l.SiteId, l.Barcode }).IsUnique();
        // Serves: the default sort, walk order within a site, as one keyset index ending with the id.
        builder.HasIndex(l => new { l.SiteId, l.WalkSequence, l.Id });
        // Serves: the zone filter and the zone's own listings.
        builder.HasIndex(l => new { l.ZoneId, l.WalkSequence, l.Id });
    }
}
