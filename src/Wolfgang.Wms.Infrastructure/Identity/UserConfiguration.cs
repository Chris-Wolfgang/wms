// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// Maps <see cref="User"/> to <c>core.user</c> (E9).
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>The module schema.</summary>
    public const string Schema = "core";

    /// <summary>The table name.</summary>
    public const string Table = "user";



    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, Schema);
        builder.Property(u => u.UserName).HasMaxLength(User.UserNameLength).IsRequired();
        builder.Property(u => u.UserNameNormalized).HasMaxLength(User.UserNameLength).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(User.DisplayNameLength).IsRequired();
        // Serves: sign-in by name (case-insensitive through the normalised column) and its uniqueness.
        builder.HasIndex(u => u.UserNameNormalized).IsUnique();
    }
}
