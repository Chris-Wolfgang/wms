// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Wolfgang.Wms.Infrastructure.Database.Conventions;

namespace Wolfgang.Wms.UnitTests.Database.TestModels;

/// <summary>
/// A compliant sample model: two entities in module schemas, a foreign key, a quantity and a timestamp.
/// </summary>
internal sealed class SampleModelDbContext : DbContext
{
    public SampleModelDbContext(DbContextOptions<SampleModelDbContext> options)
        : base(options)
    {
    }

    public DbSet<ZoneGroup> ZoneGroups => Set<ZoneGroup>();

    public DbSet<Container> Containers => Set<Container>();

    public DbSet<Sku> Skus => Set<Sku>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModelConventions.Configure(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ZoneGroup>().ToTable("ZoneGroup", "layout");
        modelBuilder.Entity<Container>().ToTable("Container", "picking");
        modelBuilder.Entity<Sku>().ToTable("Sku", "catalog");
        modelBuilder.Entity<Container>().HasIndex(c => c.Barcode).IsUnique();
        ModelConventions.Apply(modelBuilder, Database.ProviderName);
    }
}



public sealed class Sku : Wolfgang.Wms.Infrastructure.Database.Sync.ISyncedEntity
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public long RowVersion { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}



public sealed class ZoneGroup
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;
}



public sealed class Container
{
    public long Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Barcode { get; set; } = string.Empty;

    public decimal RequestedQty { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public long ZoneGroupId { get; set; }

    public ZoneGroup? ZoneGroup { get; set; }
}



/// <summary>
/// A model that breaks every checked convention at least once, so the verifier's messages are pinned.
/// </summary>
internal sealed class BadModelDbContext : DbContext
{
    public BadModelDbContext(DbContextOptions<BadModelDbContext> options)
        : base(options)
    {
    }

    public DbSet<BadParent> Parents => Set<BadParent>();

    public DbSet<BadChild> Children => Set<BadChild>();

    public DbSet<BadSoft> Softs => Set<BadSoft>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModelConventions.Configure(configurationBuilder);
        // EF re-creates a foreign key's index at model finalisation; drop that convention so the model can lack one.
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BadParent>().ToTable("BadParent", "dbo");
        modelBuilder.Entity<BadChild>().ToTable("BadChild", "picking");
        modelBuilder.Entity<BadSoft>().ToTable("BadSoft", "picking");
        modelBuilder.Entity<BadSoft>().Ignore(s => s.DeletedAt);
        modelBuilder.Entity<BadChild>().Property(c => c.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<BadChild>().Property(c => c.When).HasPrecision(7);
        modelBuilder.Entity<BadChild>().HasOne(c => c.Parent).WithMany().HasForeignKey(c => c.Owner).OnDelete(DeleteBehavior.Cascade);
        ModelConventions.Apply(modelBuilder, Database.ProviderName);
        modelBuilder.Entity<BadChild>().Metadata.GetForeignKeys().Single().DeleteBehavior = DeleteBehavior.Cascade;
    }
}



public sealed class BadParent
{
    public Guid Id { get; set; }

    public DateTime Created { get; set; }
}



public sealed class BadChild
{
    public long Id { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset When { get; set; }

    public Guid Owner { get; set; }

    public BadParent? Parent { get; set; }
}



public sealed class BadSoft : Wolfgang.Wms.Infrastructure.Database.Sync.ISoftDeletable
{
    public long Id { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
