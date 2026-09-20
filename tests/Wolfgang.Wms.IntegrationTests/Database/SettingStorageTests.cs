// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Database.Settings;
using Wolfgang.Wms.Infrastructure.Database.Sync;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E6.2 against real engines through the shipped migrations: <c>core.setting</c> takes rows through EF,
/// assigns <c>row_version</c> on insert and reassigns it on update (the migration's trigger, with EF reading
/// it back on SQL Server despite the trigger), refuses a second row for the same scope and key, and hides
/// soft-deleted rows from ordinary reads while deltas still see them.
/// </summary>
public sealed class SettingStorageTests
{
    [DockerFact]
    public async Task SqlServer_stores_settings_per_scope_and_key()
    {
        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync();

        await AssertStorageAsync(new DatabaseOptions { Provider = "SqlServer", ConnectionString = container.GetConnectionString(), TrustServerCertificate = true });
    }



    [DockerFact]
    public async Task PostgreSql_stores_settings_per_scope_and_key()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertStorageAsync(new DatabaseOptions { Provider = "PostgreSql", ConnectionString = container.GetConnectionString() });
    }



    private static async Task AssertStorageAsync(DatabaseOptions options)
    {
        var builder = new DbContextOptionsBuilder<WmsDbContext>();
        DatabaseServiceCollectionExtensions.Configure(builder, options);
        using var context = new WmsDbContext(builder.Options);
        await context.Database.MigrateAsync();

        var organisation = NewSetting("organization", 0, "picking.lease_timeout", "00:15:00");
        var site = NewSetting("site", 1, "picking.lease_timeout", "00:10:00");
        context.Settings.AddRange(organisation, site);
        await context.SaveChangesAsync();
        Assert.True(organisation.RowVersion > 0 && site.RowVersion > organisation.RowVersion, "insert assigns increasing row versions");

        var inserted = site.RowVersion;
        site.EffectiveValue = "00:12:00";
        await context.SaveChangesAsync();
        Assert.True(site.RowVersion > inserted, $"the trigger must reassign row_version on update; got {site.RowVersion} after {inserted}");

        context.Settings.Add(NewSetting("site", 1, "picking.lease_timeout", "00:05:00"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var toDelete = await context.Settings.SingleAsync(s => s.ScopeId == 1);
        toDelete.DeletedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();
        var live = await context.Settings.CountAsync();
        var manifest = await SyncQueries.ManifestAsync(context.Settings, CancellationToken.None);
        var delta = await SyncQueries.DeltaAsync(context.Settings, 0, 10, s => s.ScopeId, CancellationToken.None);

        Assert.Equal(1, live);
        Assert.Single(manifest);
        Assert.Equal([0, 1], delta.Items.Order());
    }



    private static Setting NewSetting(string scopeType, long scopeId, string key, string value)
    {
        return new Setting
        {
            ScopeType = scopeType,
            ScopeId = scopeId,
            Key = key,
            ConfiguredValue = value,
            EffectiveValue = value,
            UpdatedBy = "test",
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}
