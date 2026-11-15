// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// Maps <see cref="LocalLoginGate"/> to <c>core.local_login_gate</c> (E9.3): one row, held to one by the unique
/// index on <c>singleton</c> and the check that it is always the same value.
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

        builder.ToTable(Table, Schema, table => table.HasCheckConstraint("ck_local_login_gate_singleton", $"singleton = {LocalLoginGate.SingletonValue}"));
        builder.HasIndex(g => g.Singleton).IsUnique().HasDatabaseName("ux_local_login_gate_singleton");
        builder.Property(g => g.SsoVerifiedProvider).HasMaxLength(LocalLoginGate.ProviderLength);
        builder.Property(g => g.UnlockedBy).HasMaxLength(LocalLoginGate.ActorLength);
        builder.Property(g => g.LockedBy).HasMaxLength(LocalLoginGate.ActorLength);
        builder.Property(g => g.Signature).HasMaxLength(64);
    }
}
