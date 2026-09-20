// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Wolfgang.AuditTrail;
using Wolfgang.Wms.Infrastructure.Database.Auditing;
using Wolfgang.Wms.Infrastructure.Database.Conventions;
using Wolfgang.Wms.Infrastructure.Database.Settings;
using Wolfgang.Wms.Infrastructure.Identity;
using Wolfgang.Wms.Infrastructure.Database.Leader;
using Wolfgang.Wms.Infrastructure.Integrity;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// The one EF Core context (ADR 0002): one instance per request or job, behind <c>IUnitOfWork</c> and the
/// repositories, never used by handlers. The model is shared by both providers (E2.3) and shaped by
/// <see cref="ModelConventions"/> (E3); each module's entities are registered here explicitly (no assembly
/// scanning), with the story that adds them. It is an <see cref="AuditingDbContext"/> (E6.4): every save of
/// an audited entity writes its audit rows in the same transaction.
/// </summary>
public sealed class WmsDbContext : AuditingDbContext, IDataProtectionKeyContext
{
    /// <summary>
    /// Creates the context in a host, with the request's user provider and the shared audit options.
    /// </summary>
    public WmsDbContext(DbContextOptions<WmsDbContext> options, IAuditUserProvider userProvider, AuditOptions auditOptions)
        : base(options, userProvider, auditOptions)
    {
    }



    /// <summary>
    /// Creates the context outside a host (design time, the migrate tool, tests): changes are attributed to
    /// <see cref="WmsAuditing.SystemIdentity"/>.
    /// </summary>
    public WmsDbContext(DbContextOptions<WmsDbContext> options)
        : this(options, SystemAuditUserProvider.Instance, WmsAuditing.Options())
    {
    }



    /// <summary>
    /// <c>core.setting</c> (E6.2): setting values per scope.
    /// </summary>
    public DbSet<Setting> Settings => Set<Setting>();



    /// <summary>
    /// <c>wms.data_protection_key</c> (E8.6): the Data Protection key ring every instance shares.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();



    /// <summary>
    /// <c>core.user</c> (E9): console users.
    /// </summary>
    public DbSet<User> Users => Set<User>();



    /// <summary>
    /// <c>core.role</c> (E10.2): roles built from the permission catalog.
    /// </summary>
    public DbSet<Role> Roles => Set<Role>();



    /// <summary>
    /// <c>core.user_role</c> (E10.3): role assignments per user, everywhere or per site.
    /// </summary>
    public DbSet<UserRole> UserRoles => Set<UserRole>();



    /// <summary>
    /// <c>core.group_role_mapping</c> (E11.2): a provider's groups mapped to roles.
    /// </summary>
    public DbSet<GroupRoleMapping> GroupRoleMappings => Set<GroupRoleMapping>();



    /// <inheritdoc/>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModelConventions.Configure(configurationBuilder);
    }



    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);   // E6.4: core.audit_header / core.audit_detail
        modelBuilder.ApplyConfiguration(new SettingConfiguration());   // E6.2
        modelBuilder.ApplyConfiguration(new UserConfiguration());   // E9
        var roles = new RoleConfiguration();   // E10.2, E10.3
        modelBuilder.ApplyConfiguration<Role>(roles);
        modelBuilder.ApplyConfiguration<RolePermission>(roles);
        modelBuilder.ApplyConfiguration<UserRole>(roles);
        modelBuilder.ApplyConfiguration(new GroupRoleMappingConfiguration());   // E11.2
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_key", DatabaseServiceCollectionExtensions.HistorySchema);   // E8.6: library-owned, in wms like the migrations history
        modelBuilder.Entity<DataProtectionKey>().Property(k => k.FriendlyName).HasMaxLength(256);
        modelBuilder.Entity<IntegrityKey>().ToTable("integrity_key", DatabaseServiceCollectionExtensions.HistorySchema);   // E10.4: the HMAC key, protected by the ring
        modelBuilder.Entity<IntegrityKey>().Property(k => k.ProtectedKey).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<LeaderLock>().ToTable("leader_lock", DatabaseServiceCollectionExtensions.HistorySchema);   // E12.6: one row per singleton job
        modelBuilder.Entity<LeaderLock>().Property(l => l.Name).HasMaxLength(LeaderLock.NameLength).IsRequired();
        modelBuilder.Entity<LeaderLock>().Property(l => l.Holder).HasMaxLength(LeaderLock.HolderLength).IsRequired();
        modelBuilder.Entity<LeaderLock>().HasIndex(l => l.Name).IsUnique();   // Serves: take-or-renew by name; the first taker's insert wins
        ModelConventions.Apply(modelBuilder, Database.ProviderName);
    }
}
