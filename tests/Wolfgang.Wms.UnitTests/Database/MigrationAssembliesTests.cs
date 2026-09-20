// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.UnitTests.Database;

/// <summary>
/// E2.4: one migration assembly per provider, both carrying the same migrations (by name: the timestamp
/// prefix differs by the seconds between the two <c>dotnet ef</c> runs), and each provider's context reads
/// only its own assembly.
/// </summary>
public sealed class MigrationAssembliesTests
{
    [Fact]
    public void Both_providers_ship_the_same_migrations()
    {
        var sqlServer = MigrationIdsIn(DatabaseServiceCollectionExtensions.SqlServerMigrationsAssembly);
        var postgres = MigrationIdsIn(DatabaseServiceCollectionExtensions.PostgreSqlMigrationsAssembly);

        Assert.NotEmpty(sqlServer);
        Assert.Equal(sqlServer.Select(NameOf), postgres.Select(NameOf));
    }



    [Theory]
    [InlineData("SqlServer", DatabaseServiceCollectionExtensions.SqlServerMigrationsAssembly)]
    [InlineData("PostgreSql", DatabaseServiceCollectionExtensions.PostgreSqlMigrationsAssembly)]
    public void Each_provider_reads_migrations_from_its_own_assembly(string provider, string assembly)
    {
        var builder = new DbContextOptionsBuilder<WmsDbContext>();
        DatabaseServiceCollectionExtensions.Configure(builder, new DatabaseOptions { Provider = provider, ConnectionString = "Server=x;Host=x" });
        using var context = new WmsDbContext(builder.Options);

        var migrations = context.Database.GetMigrations().ToList();

        Assert.Equal(MigrationIdsIn(assembly), migrations);
        Assert.Equal(assembly, context.GetService<IMigrationsAssembly>().Assembly.GetName().Name);
    }



    /// <summary>
    /// The migration name without its timestamp prefix (<c>20260920025830_Initial</c> → <c>Initial</c>).
    /// </summary>
    private static string NameOf(string migrationId)
    {
        return migrationId[(migrationId.IndexOf('_', StringComparison.Ordinal) + 1)..];
    }



    private static List<string> MigrationIdsIn(string assemblyName)
    {
        return System.Reflection.Assembly.Load(assemblyName)
            .GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.GetCustomAttributes(typeof(MigrationAttribute), inherit: false).Cast<MigrationAttribute>().Single().Id)
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
