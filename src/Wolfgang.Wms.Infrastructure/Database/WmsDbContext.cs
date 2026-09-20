// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.AuditTrail;
using Wolfgang.Wms.Infrastructure.Database.Auditing;
using Wolfgang.Wms.Infrastructure.Database.Conventions;
using Wolfgang.Wms.Infrastructure.Database.Settings;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// The one EF Core context (ADR 0002): one instance per request or job, behind <c>IUnitOfWork</c> and the
/// repositories, never used by handlers. The model is shared by both providers (E2.3) and shaped by
/// <see cref="ModelConventions"/> (E3); each module's entities are registered here explicitly (no assembly
/// scanning), with the story that adds them. It is an <see cref="AuditingDbContext"/> (E6.4): every save of
/// an audited entity writes its audit rows in the same transaction.
/// </summary>
public sealed class WmsDbContext : AuditingDbContext
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
        ModelConventions.Apply(modelBuilder, Database.ProviderName);
    }
}
