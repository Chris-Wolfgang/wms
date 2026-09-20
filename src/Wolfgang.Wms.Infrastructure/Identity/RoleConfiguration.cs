// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// Maps <see cref="Role"/>, <see cref="RolePermission"/> and <see cref="UserRole"/> to <c>core.role</c>,
/// <c>core.role_permission</c> and <c>core.user_role</c> (E10.2, E10.3).
/// </summary>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>, IEntityTypeConfiguration<RolePermission>, IEntityTypeConfiguration<UserRole>
{
    /// <summary>The module schema.</summary>
    public const string Schema = "core";



    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("role", Schema);
        builder.Property(r => r.Name).HasMaxLength(Role.NameLength).IsRequired();
        builder.Property(r => r.NameNormalized).HasMaxLength(Role.NameLength).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(Role.DescriptionLength).IsRequired();
        builder.Property(r => r.BuiltInKey).HasMaxLength(Role.BuiltInKeyLength);
        builder.Property(r => r.UpdatedBy).HasMaxLength(256).IsRequired();
        builder.Property(r => r.Signature).HasMaxLength(64);
        // Serves: the role editor's name check and its uniqueness (case-insensitive through the normalised column).
        builder.HasIndex(r => r.NameNormalized).IsUnique();
        // Serves: the seeder's lookup of each built-in role.
        builder.HasIndex(r => r.BuiltInKey).IsUnique();
        builder.HasMany(r => r.Permissions).WithOne().HasForeignKey(p => p.RoleId);
    }



    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("role_permission", Schema);
        builder.Property(p => p.PermissionName).HasMaxLength(RolePermission.PermissionNameLength).IsRequired();
        // Serves: one row per permission per role.
        builder.HasIndex(p => new { p.RoleId, p.PermissionName }).IsUnique();
    }



    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_role", Schema);
        builder.Property(a => a.UpdatedBy).HasMaxLength(256).IsRequired();
        builder.Property(a => a.Signature).HasMaxLength(64);
        builder.HasOne(a => a.Role).WithMany().HasForeignKey(a => a.RoleId);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId);
        // Serves: sign-in reading a user's assignments, and one row per role per scope.
        builder.HasIndex(a => new { a.UserId, a.RoleId, a.SiteId }).IsUnique();
    }
}
