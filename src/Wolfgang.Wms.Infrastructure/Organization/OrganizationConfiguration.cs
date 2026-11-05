// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wolfgang.Wms.Core.Organization;

namespace Wolfgang.Wms.Infrastructure.Organization;

/// <summary>
/// Maps <see cref="Organization"/> to <c>core.organization</c> (E16.0): one row, no natural key, the lengths
/// from <see cref="OrganizationRules"/>.
/// </summary>
public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    /// <summary>The module schema.</summary>
    public const string Schema = "core";

    /// <summary>The table name.</summary>
    public const string Table = "organization";



    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, Schema);
        builder.Property(o => o.Name).HasMaxLength(OrganizationRules.NameLength).IsRequired();
        builder.Property(o => o.LegalName).HasMaxLength(OrganizationRules.LegalNameLength);
        builder.Property(o => o.LogoDataUrl).HasMaxLength(OrganizationRules.LogoLength);
        builder.Property(o => o.TimeZone).HasMaxLength(OrganizationRules.IdLength).IsRequired();
        builder.Property(o => o.Locale).HasMaxLength(OrganizationRules.IdLength).IsRequired();
        builder.Property(o => o.AddressLine1).HasMaxLength(OrganizationRules.TextLength);
        builder.Property(o => o.AddressLine2).HasMaxLength(OrganizationRules.TextLength);
        builder.Property(o => o.AddressCity).HasMaxLength(OrganizationRules.TextLength);
        builder.Property(o => o.AddressRegion).HasMaxLength(OrganizationRules.TextLength);
        builder.Property(o => o.AddressPostalCode).HasMaxLength(OrganizationRules.PostalCodeLength);
        builder.Property(o => o.AddressCountry).HasMaxLength(OrganizationRules.TextLength);
        builder.Property(o => o.PrimaryContactName).HasMaxLength(OrganizationRules.TextLength);
        builder.Property(o => o.PrimaryContactEmail).HasMaxLength(OrganizationRules.ContactLength);
        builder.Property(o => o.PrimaryContactPhone).HasMaxLength(OrganizationRules.ContactLength);
        builder.Property(o => o.SupportContactName).HasMaxLength(OrganizationRules.TextLength);
        builder.Property(o => o.SupportContactEmail).HasMaxLength(OrganizationRules.ContactLength);
        builder.Property(o => o.SupportContactPhone).HasMaxLength(OrganizationRules.ContactLength);
        builder.Property(o => o.UpdatedBy).HasMaxLength(256).IsRequired();
    }
}
