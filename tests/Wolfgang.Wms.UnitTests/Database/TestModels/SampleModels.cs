// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Wolfgang.Wms.Infrastructure.Database.Conventions;

namespace Wolfgang.Wms.UnitTests.Database.TestModels;

/// <summary>
/// A compliant sample model: two entities in module schemas, a foreign key, a quantity and a timestamp, an
/// owned value sharing its owner's table and an owned collection in a table of its own.
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
        modelBuilder.Entity<Container>().OwnsOne(c => c.ShipTo);
        modelBuilder.Entity<Container>().OwnsMany(c => c.Lines, line => line.ToTable("ContainerLine", "picking"));
        ModelConventions.Apply(modelBuilder, Database.ProviderName);
    }
}



public sealed class Sku : Wolfgang.Wms.Infrastructure.Database.IVersionedEntity
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public long RowVersion { get; set; }
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

    public Address ShipTo { get; set; } = new();

    public List<ContainerLine> Lines { get; } = [];
}



public sealed class Address
{
    public string Street { get; set; } = string.Empty;

    public DateTimeOffset VerifiedAt { get; set; }
}



public sealed class ContainerLine
{
    public long Id { get; set; }

    public decimal Qty { get; set; }
}



/// <summary>
/// A module that names a table explicitly (<c>ToTable("PickTask", "picking")</c> on a class called
/// <see cref="WorkItem"/>) through a DbSet whose property name is a third spelling: the explicit name wins,
/// snake_cased, and the DbSet name is never used.
/// </summary>
internal sealed class ExplicitNamesDbContext : DbContext
{
    public ExplicitNamesDbContext(DbContextOptions<ExplicitNamesDbContext> options)
        : base(options)
    {
    }

    public DbSet<WorkItem> OpenWork => Set<WorkItem>();

    public DbSet<Bin> Bins => Set<Bin>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModelConventions.Configure(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkItem>().ToTable("PickTask", "picking");
        modelBuilder.Entity<Bin>().ToTable("Bin", "layout");
        ModelConventions.Apply(modelBuilder, Database.ProviderName);
    }
}



internal sealed class WorkItem
{
    public long Id { get; set; }

    public string Code { get; set; } = "";
}



internal sealed class Bin
{
    public long Id { get; set; }

    public string Code { get; set; } = "";
}



/// <summary>
/// A compliant model whose convention-built index name runs past PostgreSQL's 63 characters (E3.2): the one
/// violation the verifier must report on both providers before a migration is generated.
/// </summary>
internal sealed class LongNamesDbContext : DbContext
{
    public LongNamesDbContext(DbContextOptions<LongNamesDbContext> options)
        : base(options)
    {
    }

    public DbSet<InventoryAdjustmentReconciliationLine> Lines => Set<InventoryAdjustmentReconciliationLine>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModelConventions.Configure(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("picking");
        modelBuilder.Entity<InventoryAdjustmentReconciliationLine>().HasIndex(l => new { l.WarehouseLocationIdentifier, l.ReconciliationBatchIdentifier });
        ModelConventions.Apply(modelBuilder, Database.ProviderName);
    }
}



/// <summary>
/// A 40-character table name; with its two long columns the conventional index name is 105 characters.
/// </summary>
internal sealed class InventoryAdjustmentReconciliationLine
{
    public long Id { get; set; }

    public string WarehouseLocationIdentifier { get; set; } = "";

    public string ReconciliationBatchIdentifier { get; set; } = "";
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

    public DbSet<BadGenerated> Generated => Set<BadGenerated>();

    public DbSet<BadFactoryGenerated> FactoryGenerated => Set<BadFactoryGenerated>();

    public DbSet<BadDefaulted> Defaulted => Set<BadDefaulted>();

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
        modelBuilder.Entity<BadChild>().Property(c => c.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<BadChild>().Property(c => c.When).HasPrecision(7);
        modelBuilder.Entity<BadChild>().HasOne(c => c.Parent).WithMany().HasForeignKey(c => c.Owner).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<BadChild>().HasAlternateKey(c => c.Code);
        modelBuilder.Entity<BadChild>().HasIndex(c => c.Ref).IsUnique();
        modelBuilder.Entity<BadChild>().HasIndex(c => c.Amount);
        modelBuilder.Entity<BadChild>().OwnsOne(c => c.Stamp);
        modelBuilder.Entity<BadChild>().OwnsMany(c => c.Lines, line => line.ToTable("BadLine", "dbo"));
        modelBuilder.Entity<BadGenerated>().ToTable("BadGenerated", "picking");
        modelBuilder.Entity<BadGenerated>().Property(g => g.Id).HasValueGenerator<ClientIdGenerator>();
        modelBuilder.Entity<BadFactoryGenerated>().ToTable("BadFactoryGenerated", "picking");
        modelBuilder.Entity<BadFactoryGenerated>().Property(g => g.Id).HasValueGeneratorFactory<ClientIdGeneratorFactory>();
        modelBuilder.Entity<BadDefaulted>().ToTable("BadDefaulted", "picking");
        modelBuilder.Entity<BadDefaulted>().Property(d => d.Id).HasDefaultValueSql("1");   // a default, not an identity
        modelBuilder.Entity<BadDefaulted>().OwnsOne(d => d.Note);
        modelBuilder.Entity<BadDefaulted>().OwnsMany
        (
            d => d.Items,
            item =>
            {
                item.ToTable("BadDefaultedItem", "picking");
                item.Property(i => i.Id).ValueGeneratedNever();   // the collection's own id assigned by the client
            }
        );
        ModelConventions.Apply(modelBuilder, Database.ProviderName);

        // Names a module could still override after the conventions ran: wrong prefix, or not snake_case.
        modelBuilder.Entity<BadFactoryGenerated>().Metadata.SetTableName("BadFactoryGenerated");
        var child = modelBuilder.Entity<BadChild>().Metadata;
        child.FindPrimaryKey()!.SetName("pk_BadChild");
        child.FindNavigation(nameof(BadChild.Stamp))!.TargetEntityType.FindPrimaryKey()!.SetName("pk_BadChild");   // shares the table
        child.GetKeys().Single(k => !k.IsPrimaryKey()).SetName("uk_bad_child_code");
        child.GetForeignKeys().Single(fk => !fk.IsOwnership).SetConstraintName("FK_bad_child_owner");
        child.GetIndexes().Single(i => i.IsUnique).SetDatabaseName("ix_bad_child_ref");
        child.GetIndexes().Single(i => !i.IsUnique).SetDatabaseName("ux_bad_child_amount");
        child.FindProperty(nameof(BadChild.Ref))!.SetColumnName("Ref");
        child.GetForeignKeys().Single(fk => !fk.IsOwnership).DeleteBehavior = DeleteBehavior.Cascade;
        child.FindNavigation(nameof(BadChild.Lines))!.TargetEntityType.FindOwnership()!.DeleteBehavior = DeleteBehavior.Cascade;

        // A table-split owned value has no constraint, but its ownership still needs ClientCascade.
        modelBuilder.Entity<BadDefaulted>().Metadata.FindNavigation(nameof(BadDefaulted.Note))!.TargetEntityType.FindOwnership()!.DeleteBehavior = DeleteBehavior.Restrict;
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

    public string Code { get; set; } = string.Empty;

    public string Ref { get; set; } = string.Empty;

    public BadStamp Stamp { get; set; } = new();

    public List<BadLine> Lines { get; } = [];
}



public sealed class BadStamp
{
    public DateTime At { get; set; }
}



public sealed class BadLine
{
    public int Id { get; set; }
}



public sealed class BadGenerated
{
    public long Id { get; set; }
}



public sealed class BadFactoryGenerated
{
    public long Id { get; set; }
}



public sealed class BadDefaulted
{
    public long Id { get; set; }

    public BadNote Note { get; set; } = new();

    public List<BadItem> Items { get; } = [];
}



public sealed class BadNote
{
    public string Text { get; set; } = string.Empty;
}



public sealed class BadItem
{
    public long Id { get; set; }
}



/// <summary>
/// A client-side id generator: the kind of key configuration the verifier rejects. Never run.
/// </summary>
internal sealed class ClientIdGenerator : ValueGenerator<long>
{
    public override bool GeneratesTemporaryValues => false;

    public override long Next(EntityEntry entry)
    {
        return 1;
    }
}



/// <summary>
/// A factory for <see cref="ClientIdGenerator"/>, the other way a module could configure client-side ids.
/// </summary>
internal sealed class ClientIdGeneratorFactory : ValueGeneratorFactory
{
    public override ValueGenerator Create(IProperty property, ITypeBase typeBase)
    {
        return new ClientIdGenerator();
    }
}
