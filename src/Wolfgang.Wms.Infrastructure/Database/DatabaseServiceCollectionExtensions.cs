// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Caching;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Schema;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Infrastructure.Database.Auditing;
using Wolfgang.Wms.Infrastructure.Database.Settings;
using Wolfgang.Wms.Core.Secrets;
using Wolfgang.Wms.Infrastructure.Secrets;
using Microsoft.AspNetCore.Identity;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Identity.External;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wolfgang.Wms.Core.Jobs;
using Wolfgang.Wms.Infrastructure.Database.Health;
using Wolfgang.Wms.Infrastructure.Database.Leader;
using Wolfgang.Wms.Infrastructure.Identity;
using Wolfgang.Wms.Infrastructure.Integrity;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// Registers the database for the provider the installer configured (E2.1).
/// </summary>
public static class DatabaseServiceCollectionExtensions
{
    /// <summary>
    /// Assembly holding the SQL Server migrations (E2.4); referenced by the hosts that migrate or report schema.
    /// </summary>
    public const string SqlServerMigrationsAssembly = "Wolfgang.Wms.Infrastructure.Migrations.SqlServer";



    /// <summary>
    /// Assembly holding the PostgreSQL migrations (E2.4).
    /// </summary>
    public const string PostgreSqlMigrationsAssembly = "Wolfgang.Wms.Infrastructure.Migrations.PostgreSql";



    /// <summary>
    /// Schema of the migrations history table (E3.1: nothing in dbo/public; E4.5: created by the first migration
    /// with CREATE SCHEMA rights only).
    /// </summary>
    public const string HistorySchema = "wms";



    /// <summary>
    /// Name of the migrations history table.
    /// </summary>
    public const string HistoryTable = "migrations_history";



    /// <summary>
    /// Binds and validates <see cref="DatabaseOptions"/> (startup fails on an unknown provider or a missing
    /// connection string), registers <see cref="WmsDbContext"/> on the chosen provider, and replaces the
    /// bootstrap schema source with the migrations-history one. With provider <c>None</c> no context is
    /// registered and the schema endpoint keeps reporting no database.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IServiceCollection AddWmsDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>());

        services.AddExceptionHandler<ConcurrencyExceptionHandler>();   // E5.2: stale save → 412, like a failed If-Match

        var options = new DatabaseOptions();
        configuration.GetSection(DatabaseOptions.SectionName).Bind(options);
        if (options.ParsedProvider is null or DatabaseProvider.None)
        {
            return services;
        }

        services.AddWmsAuditing();   // E6.4: AuditTrail options and the on-behalf-of user provider the context needs
        services.AddHostedService<SecretsStartupCheck>();   // E8.2: an encrypted connection string that cannot be decrypted fails here, clearly
        services.AddWmsIntegritySigning();   // E10.4: rows are signed on save
        services.AddDbContext<WmsDbContext>((provider, builder) =>
        {
            var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            // The protector is resolved only for an encrypted string: the database ring (E8.6) would need this very context.
            Configure(builder, database, database.ConnectionStringIsProtected ? provider.GetRequiredService<ISecretProtector>() : null);
            builder.AddInterceptors(provider.GetRequiredService<IntegritySigningInterceptor>());
        });
        services.RemoveAll<ISchemaVersionSource>();
        services.AddScoped<ISchemaVersionSource, MigrationsSchemaVersionSource>();
        services.AddScoped<MigrationRunner>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>(DatabaseHealthCheck.Name, failureStatus: HealthStatus.Unhealthy, tags: [Core.Hosting.WmsHealth.ReadyTag]);   // E12.1: readiness = reachable + at the build's schema
        services.RemoveAll<ILeaderLock>();
        services.AddSingleton<ILeaderLock, EfLeaderLock>();   // E12.6: singleton jobs run under a database lease
        services.AddWmsModules();
        services.TryAddSingleton(provider => new SettingRegistry(provider.GetRequiredService<ModuleCollection>()));   // hosts without the settings module (the worker) still get the accessor
        services.TryAddSingleton<ISettingScopeHierarchy, OrganizationOnlyScopeHierarchy>();
        services.TryAddSingleton(provider => new PermissionCatalog(provider.GetRequiredService<ModuleCollection>()));   // the roles store's catalog, for hosts without the auth module
        services.TryAddSingleton<IRowVersionSource, MaxRowVersionSource>();   // E1.12: the caches' one invalidation signal
        services.TryAddSingleton<SettingsCache>();
        services.RemoveAll<ISettings>();
        services.AddScoped<ISettings, EfSettings>();   // E6.3: the stored accessor replaces the defaults-only one
        services.AddHostedService<SchemaStartupCheck>();   // E4.4: refuse to start on a schema that is behind or ahead
        AddIdentity(services, configuration);   // E9, E10: accounts, roles, sessions, integrity
        return services;
    }



    private static void AddIdentity(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<IntegrityBackfillCheck>();   // E10.4: before any security row is written, the key exists and pre-existing rows are signed
        services.AddOptions<BootstrapOptions>().Bind(configuration.GetSection(BootstrapOptions.SectionName));
        services.TryAddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.RemoveAll<ILocalAccounts>();
        services.AddScoped<ILocalAccounts, EfLocalAccounts>();   // E9: the stored accounts replace the placeholder
        services.AddHostedService<BootstrapAdminCheck>();   // E9.1: after the schema check, the administrator exists
        services.RemoveAll<IRoles>();
        services.AddScoped<IRoles, EfRoles>();   // E10.2: the stored roles replace the placeholder
        services.RemoveAll<ISessionRevocations>();
        services.AddScoped<ISessionRevocations, EfSessionRevocations>();   // E10.5: per-user "sessions valid after"
        services.RemoveAll<IExternalAccounts>();
        services.AddScoped<IExternalAccounts, EfExternalAccounts>();   // E11.1: provider accounts in core.user
        services.RemoveAll<IGroupRoleMappings>();
        services.AddScoped<IGroupRoleMappings, EfGroupRoleMappings>();   // E11.2: directory groups → roles
        services.AddHostedService<BuiltInRolesCheck>();   // E10.2: built-in roles follow the catalog; local administrators hold Administrator
    }



    /// <summary>
    /// Points the context at the configured provider. Called per context; the options are already validated.
    /// </summary>
    /// <exception cref="InvalidOperationException">The provider is not one a context can run on.</exception>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, DatabaseOptions options)
    {
        return Configure(builder, options, protector: null);
    }



    /// <summary>
    /// Configures the provider, decrypting an <c>enc:v1:</c> connection string with <paramref name="protector"/>
    /// (E8.2).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The provider cannot host a context, or the connection string is encrypted and cannot be decrypted.</exception>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, DatabaseOptions options, ISecretProtector? protector)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        return options.ParsedProvider switch
        {
            // Retry on transient failures (E6.4: the audited context owns the strategy wrap, so this is safe);
            // PostgreSQL batches are capped at 100 rows, where AuditTrail's own benchmarks stop paying off.
            DatabaseProvider.SqlServer => builder.UseSqlServer(options.EffectiveConnectionString(protector), sql => sql.MigrationsAssembly(SqlServerMigrationsAssembly).MigrationsHistoryTable(HistoryTable, HistorySchema).EnableRetryOnFailure()),
            DatabaseProvider.PostgreSql => builder.UseNpgsql(options.EffectiveConnectionString(protector), npgsql => npgsql.MigrationsAssembly(PostgreSqlMigrationsAssembly).MigrationsHistoryTable(HistoryTable, HistorySchema).EnableRetryOnFailure().MaxBatchSize(WmsAuditing.PostgreSqlMaxBatchSize)),
            _ => throw new InvalidOperationException($"{DatabaseOptions.SectionName}:Provider '{options.Provider}' cannot host a database context."),
        };
    }
}
