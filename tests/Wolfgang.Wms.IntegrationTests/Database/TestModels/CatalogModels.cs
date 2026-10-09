// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Infrastructure.Database.Conventions;

namespace Wolfgang.Wms.IntegrationTests.Database.TestModels;

/// <summary>
/// A compliant model created on a real engine so the catalogs can be inspected: two module schemas, a
/// cross-schema foreign key, a unique natural key, a quantity, timestamps, an owned value sharing its owner's
/// table and an owned collection in a table of its own. The product model has no entities yet (they arrive
/// with the module stories), so this stands in for it.
/// </summary>
internal sealed class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
        : base(options)
    {
    }



    public DbSet<ZoneGroup> ZoneGroups => Set<ZoneGroup>();



    public DbSet<Container> Containers => Set<Container>();



    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModelConventions.Configure(configurationBuilder);
    }



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ZoneGroup>().ToTable("ZoneGroup", "layout");
        modelBuilder.Entity<Container>().ToTable("Container", "picking");
        modelBuilder.Entity<Container>().Property(c => c.Barcode).HasMaxLength(32);
        modelBuilder.Entity<Container>().HasIndex(c => c.Barcode).IsUnique();
        modelBuilder.Entity<Container>().HasOne(c => c.ZoneGroup).WithMany().HasForeignKey(c => c.ZoneGroupId);
        modelBuilder.Entity<Container>().OwnsOne(c => c.ShipTo);
        modelBuilder.Entity<Container>().OwnsMany(c => c.Lines, line => line.ToTable("ContainerLine", "picking"));
        ModelConventions.Apply(modelBuilder, Database.ProviderName);
    }
}



public sealed class ZoneGroup
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;
}



public sealed class Container
{
    public long Id { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public decimal RequestedQty { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public long ZoneGroupId { get; set; }

    public ZoneGroup? ZoneGroup { get; set; }

    public Address ShipTo { get; set; } = new();

    public List<ContainerLine> Lines { get; } = [];
}



public sealed class Address
{
    public string Street { get; set; } = string.Empty;
}



public sealed class ContainerLine
{
    public long Id { get; set; }

    public decimal Qty { get; set; }
}
