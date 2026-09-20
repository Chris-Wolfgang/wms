// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Migrations.SqlServer;

/// <summary>
/// Lets <c>dotnet ef</c> build the context for this provider without a running server (E2.4). The same
/// <see cref="DatabaseServiceCollectionExtensions.Configure"/> the host uses, so design time and run time
/// agree on the provider and the migrations assembly.
/// </summary>
public sealed class SqlServerDesignTimeContextFactory : IDesignTimeDbContextFactory<WmsDbContext>
{
    /// <inheritdoc/>
    public WmsDbContext CreateDbContext(string[] args)
    {
        var options = new DatabaseOptions
        {
            Provider = nameof(DatabaseProvider.SqlServer),
            ConnectionString = "Server=localhost;Database=wms-design;Encrypt=False",
        };
        var builder = new DbContextOptionsBuilder<WmsDbContext>();
        DatabaseServiceCollectionExtensions.Configure(builder, options);
        return new WmsDbContext(builder.Options);
    }
}
