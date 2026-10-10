// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// Maps <see cref="GroupRoleMapping"/> to <c>core.group_role_mapping</c> (E11.2).
/// </summary>
public sealed class GroupRoleMappingConfiguration : IEntityTypeConfiguration<GroupRoleMapping>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<GroupRoleMapping> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("group_role_mapping", RoleConfiguration.Schema);
        builder.Property(m => m.Provider).HasMaxLength(GroupRoleMapping.ProviderLength).IsRequired();
        builder.Property(m => m.GroupKey).HasMaxLength(GroupRoleMapping.GroupLength).IsRequired();
        builder.Property(m => m.UpdatedBy).HasMaxLength(256).IsRequired();
        builder.Property(m => m.Signature).HasMaxLength(64);
        builder.HasOne(m => m.Role).WithMany().HasForeignKey(m => m.RoleId);
        // Serves: sign-in reading a provider's mappings for the groups a user is in; the store checks duplicates.
        builder.HasIndex(m => new { m.Provider, m.GroupKey, m.RoleId, m.SiteId });
        // Serves: the role's foreign key.
        builder.HasIndex(m => m.RoleId);
    }
}
