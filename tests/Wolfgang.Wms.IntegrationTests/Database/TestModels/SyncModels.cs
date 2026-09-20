// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Infrastructure.Database.Conventions;
using Wolfgang.Wms.Infrastructure.Database.Sync;

namespace Wolfgang.Wms.IntegrationTests.Database.TestModels;

/// <summary>
/// A test-only synced table (no product master table exists yet) built with the product conventions.
/// </summary>
internal sealed class SyncSampleDbContext : DbContext
{
    public SyncSampleDbContext(DbContextOptions<SyncSampleDbContext> options)
        : base(options)
    {
    }

    public DbSet<SyncThing> Things => Set<SyncThing>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModelConventions.Configure(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncThing>().ToTable("SyncThing", "picking");
        ModelConventions.Apply(modelBuilder, Database.ProviderName);
    }
}



public sealed class SyncThing : ISyncedEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public long RowVersion { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}



public sealed record ThingDto(long Id, string Name, long RowVersion, DateTimeOffset? DeletedAt);



public sealed record DeltaDto(List<ThingDto> Items, long NextSince, bool HasMore);



public sealed record ManifestEntryDto(long Id, long RowVersion);
