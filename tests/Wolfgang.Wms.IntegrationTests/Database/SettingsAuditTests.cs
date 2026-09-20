// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Wolfgang.AuditTrail;
using Wolfgang.AuditTrail.Entities;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Database.Auditing;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E6.4 against real engines through the shipped migrations: a setting written through the accessor leaves
/// an audit header (service identity, on-behalf-of user, entity, key, operation, transaction) and one
/// detail per changed column in <c>core.audit_header</c> / <c>core.audit_detail</c>, in the same transaction
/// as the change; the audit tables came out snake_case in the <c>core</c> schema.
/// </summary>
public sealed class SettingsAuditTests
{
    private static readonly SettingKey<int> MaxTotes = new("sample.max_totes", 1, "Totes a picker may carry.");



    [DockerFact]
    public async Task SqlServer_setting_changes_are_audited()
    {
        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync();

        await AssertAuditedAsync(new DatabaseOptions { Provider = "SqlServer", ConnectionString = container.GetConnectionString(), TrustServerCertificate = true });
    }



    [DockerFact]
    public async Task PostgreSql_setting_changes_are_audited()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertAuditedAsync(new DatabaseOptions { Provider = "PostgreSql", ConnectionString = container.GetConnectionString() });
    }



    private static async Task AssertAuditedAsync(DatabaseOptions options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestEnvironment());
        services.AddWmsModules();
        services.AddWmsSettingsModule();
        services.AddWmsModule(ModuleDescriptor.Create("sample").WithSettings(MaxTotes));
        services.AddDbContext<WmsDbContext>(builder => DatabaseServiceCollectionExtensions.Configure(builder, options));
        services.AddWmsAuditing();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Wolfgang.Wms.Core.Caching.IRowVersionSource, MaxRowVersionSource>();
        services.AddSingleton<Wolfgang.Wms.Infrastructure.Database.Settings.SettingsCache>();
        services.AddScoped<ISettings, Wolfgang.Wms.Infrastructure.Database.Settings.EfSettings>();
        await using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<WmsDbContext>().Database.MigrateAsync();
            var settings = scope.ServiceProvider.GetRequiredService<ISettings>();
            await settings.SetAsync(MaxTotes, SettingScopeRef.Organization, 4, "alice", CancellationToken.None);
            await settings.SetAsync(MaxTotes, SettingScopeRef.Organization, 5, "alice", CancellationToken.None);
        }

        using var check = provider.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<WmsDbContext>();
        var headers = await context.Set<AuditHeader>().Include(h => h.Details).OrderBy(h => h.AuditedAtUtc).ToListAsync();
        var columns = await context.Database.SqlQueryRaw<string>("SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'core' AND table_name = 'audit_header'").ToListAsync();

        Assert.Equal([AuditOperation.Insert, AuditOperation.Update], headers.Select(h => h.Operation));
        Assert.All(headers, h => Assert.Equal("TestHost", h.UserId));
        Assert.All(headers, h => Assert.Null(h.OnBehalfOfUserId));   // no request user until E9
        Assert.All(headers, h => Assert.Contains("setting", h.EntityTable, StringComparison.Ordinal));
        Assert.Equal(headers[0].EntityKey, headers[1].EntityKey);
        Assert.NotEqual(headers[0].TransactionId, headers[1].TransactionId);
        Assert.Contains(headers[1].Details, d => string.Equals(d.ColumnName, "configured_value", StringComparison.Ordinal) && string.Equals(d.ValueText, "5", StringComparison.Ordinal));
        Assert.Contains("on_behalf_of_user_id", columns);
        Assert.Contains("audited_at_utc", columns);
        Assert.DoesNotContain(columns, c => c.Any(char.IsUpper));
    }



    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "TestHost";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
