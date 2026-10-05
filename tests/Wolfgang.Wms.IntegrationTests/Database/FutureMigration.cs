// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E4.6: makes a migrated database look as if a newer build had migrated it, by recording a migration this
/// build does not ship in <c>wms.migrations_history</c>.
/// </summary>
internal static class FutureMigration
{
    public const string Id = "20991231000000_FromANewerBuild";



    public static async Task RecordAsync(string provider, string connectionString)
    {
        var builder = new DbContextOptionsBuilder<WmsDbContext>();
        DatabaseServiceCollectionExtensions.Configure
        (
            builder,
            new DatabaseOptions
            {
                Provider = provider,
                ConnectionString = connectionString,
                TrustServerCertificate = string.Equals(provider, "SqlServer", StringComparison.Ordinal),
            }
        );
        await using var context = new WmsDbContext(builder.Options);
        var sql = string.Equals(provider, "SqlServer", StringComparison.Ordinal)
            ? "INSERT INTO [wms].[migrations_history] ([MigrationId], [ProductVersion]) VALUES (N'" + Id + "', N'99.0.0')"
            : "INSERT INTO wms.migrations_history (\"MigrationId\", \"ProductVersion\") VALUES ('" + Id + "', '99.0.0')";

        await context.Database.ExecuteSqlRawAsync(sql);
    }
}
