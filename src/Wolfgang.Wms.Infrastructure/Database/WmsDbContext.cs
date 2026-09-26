// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Infrastructure.Database.Conventions;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// The one EF Core context (ADR 0002): one instance per request or job, behind <c>IUnitOfWork</c> and the
/// repositories, never used by handlers. The model is shared by both providers (E2.3) and shaped by
/// <see cref="ModelConventions"/> (E3); entities and their module schemas arrive with the module stories.
/// </summary>
public sealed class WmsDbContext : DbContext
{
    /// <summary>
    /// Creates the context with the provider chosen by <see cref="DatabaseServiceCollectionExtensions.AddWmsDatabase"/>.
    /// </summary>
    public WmsDbContext(DbContextOptions<WmsDbContext> options)
        : base(options)
    {
    }



    /// <inheritdoc/>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModelConventions.Configure(configurationBuilder);
    }



    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ModelConventions.Apply(modelBuilder, Database.ProviderName);
    }
}
