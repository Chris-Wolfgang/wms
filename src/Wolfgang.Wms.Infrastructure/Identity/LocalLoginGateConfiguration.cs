// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// Maps <see cref="LocalLoginGate"/> to <c>core.local_login_gate</c> (E9.3): one row, no natural key.
/// </summary>
public sealed class LocalLoginGateConfiguration : IEntityTypeConfiguration<LocalLoginGate>
{
    /// <summary>The module schema.</summary>
    public const string Schema = "core";

    /// <summary>The table name.</summary>
    public const string Table = "local_login_gate";



    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LocalLoginGate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, Schema);
        builder.Property(g => g.SsoVerifiedProvider).HasMaxLength(LocalLoginGate.ProviderLength);
        builder.Property(g => g.UnlockedBy).HasMaxLength(LocalLoginGate.ActorLength);
        builder.Property(g => g.LockedBy).HasMaxLength(LocalLoginGate.ActorLength);
    }
}
