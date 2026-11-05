// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.AuditTrail;
using Wolfgang.Wms.Infrastructure.Database.Auditing;
using Wolfgang.Wms.Infrastructure.Database.Conventions;

namespace Wolfgang.Wms.IntegrationTests.Database.TestModels;

/// <summary>
/// A throwaway audited context with an aggregate: a parent with an owned address, a complex-type size and
/// cascade-deleted children, built with the product conventions.
/// </summary>
internal sealed class AuditCapabilityDbContext : AuditingDbContext
{
    public AuditCapabilityDbContext(DbContextOptions<AuditCapabilityDbContext> options)
        : base(options, SystemAuditUserProvider.Instance, WmsAuditing.Options())
    {
    }

    public DbSet<AuditedParent> Parents => Set<AuditedParent>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModelConventions.Configure(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<AuditedParent>(parent =>
        {
            parent.ToTable("AuditedParent", "picking");
            parent.OwnsOne(p => p.Address);
            parent.ComplexProperty(p => p.Size);
            parent.HasMany(p => p.Children).WithOne().HasForeignKey(c => c.AuditedParentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AuditedChild>().ToTable("AuditedChild", "picking");
        ModelConventions.Apply(modelBuilder, Database.ProviderName);
        modelBuilder.Entity<AuditedChild>().Metadata.GetForeignKeys().Single().DeleteBehavior = DeleteBehavior.Cascade;   // the conventions force Restrict; this test needs the cascade
    }
}



public sealed class AuditedParent
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public AuditedAddress Address { get; set; } = new();

    public AuditedSize Size { get; set; } = new();

    public List<AuditedChild> Children { get; set; } = [];
}



public sealed class AuditedAddress
{
    public string Street { get; set; } = string.Empty;
}



public sealed class AuditedSize
{
    public int Width { get; set; }

    public int Height { get; set; }
}



public sealed class AuditedChild
{
    public long Id { get; set; }

    public string Label { get; set; } = string.Empty;

    public long AuditedParentId { get; set; }
}
