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
using Wolfgang.Wms.Core.Identity.BreakGlass;
using Wolfgang.Wms.Core.Organization;
using Wolfgang.Wms.Core.Identity.External;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wolfgang.Wms.Core.Jobs;
using Wolfgang.Wms.Infrastructure.Database.Health;
using Wolfgang.Wms.Infrastructure.Database.Leader;
using Wolfgang.Wms.Infrastructure.Identity;
using Wolfgang.Wms.Infrastructure.Integrity;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Core.Zones;

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
    /// Schema of the migrations history table (E3.1: nothing in dbo/public). EF creates the schema together with
    /// the history table, before any migration runs (<c>IF SCHEMA_ID(N'wms') IS NULL ... CREATE SCHEMA</c> on
    /// SQL Server, <c>CREATE SCHEMA wms</c> guarded by <c>pg_namespace</c> on PostgreSQL), so an empty database
    /// needs CREATE SCHEMA rights only (E4.5). No release ever used EF's default <c>__EFMigrationsHistory</c>
    /// location, so there is no history to carry over from it.
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
        services.TryAddSingleton(provider => new PermissionCatalog(provider.GetRequiredService<ModuleCollection>()));   // the roles store's catalog, for hosts without the auth module
        services.TryAddSingleton<IRowVersionSource, MaxRowVersionSource>();   // E1.12: the caches' one invalidation signal
        services.TryAddSingleton<SettingsCache>();
        services.RemoveAll<ISettings>();
        services.AddScoped<ISettings, EfSettings>();   // E6.3: the stored accessor replaces the defaults-only one
        AddMasterData(services);   // E16: the organization, the sites and the zones, and the settings cascade over them
        services.AddHostedService<SchemaStartupCheck>();   // E4.4: refuse to start on a schema that is behind or ahead
        AddIdentity(services, configuration);   // E9, E10: accounts, roles, sessions, integrity
        return services;
    }



    /// <summary>
    /// E16: the stored organization (E16.0), sites (E16.1) and zones (E16.2) replace the placeholders, and the
    /// settings cascade runs over the stored sites and zones. The open-work answers stay the never-blocking ones
    /// until release intake (E22) and tote entry (E26) register theirs.
    /// </summary>
    private static void AddMasterData(IServiceCollection services)
    {
        UseStoredScopeHierarchy(services);   // E16.1: the cascade over the stored sites replaces the organisation-only placeholder
        services.RemoveAll<IOrganization>();
        services.AddScoped<IOrganization, Organization.EfOrganization>();   // E16.0: the stored organization replaces the placeholder
        services.RemoveAll<ISites>();
        services.AddScoped<ISites, Sites.EfSites>();   // E16.1: the stored sites replace the placeholder
        services.TryAddSingleton<IOpenReleases, NoOpenReleases>();   // E16.1: nothing blocks retiring a site until release intake (E22) answers
        services.RemoveAll<IZones>();
        services.AddScoped<IZones, Zones.EfZones>();   // E16.2: the stored zones replace the placeholder
        services.TryAddSingleton<IOpenZoneGroups, NoOpenZoneGroups>();   // E16.2: nothing blocks retiring a zone until tote entry (E26) answers
    }



    /// <summary>
    /// E16.1: the cascade over the stored sites replaces <see cref="OrganizationOnlyScopeHierarchy"/>. A host that
    /// registered a hierarchy of its own (a test host) keeps it: only the placeholder descriptor goes.
    /// </summary>
    private static void UseStoredScopeHierarchy(IServiceCollection services)
    {
        foreach (var placeholder in services.Where(d => d.ServiceType == typeof(ISettingScopeHierarchy) && d.ImplementationType == typeof(OrganizationOnlyScopeHierarchy)).ToList())
        {
            services.Remove(placeholder);
        }

        services.TryAddScoped<ISettingScopeHierarchy, EfSettingScopeHierarchy>();
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
        services.RemoveAll<ILocalLoginGate>();
        services.AddScoped<ILocalLoginGate, EfLocalLoginGate>();   // E9.3: the stored break-glass gate replaces the always-open placeholder
        services.AddHostedService<BuiltInRolesCheck>();   // E10.2: built-in roles follow the catalog; local administrators hold Administrator
    }



    /// <summary>
    /// Points the context at the configured provider. Called per context; the options are already validated.
    /// An empty connection string (only <c>wms-migrate --script</c> accepts one) configures the provider with
    /// no connection, which is enough to generate SQL.
    /// </summary>
    /// <exception cref="InvalidOperationException">The provider is not one a context can run on.</exception>
    /// <exception cref="ArgumentException">The SQL Server connection string is malformed and
    /// <see cref="DatabaseOptions.TrustServerCertificate"/> is set (it is parsed to apply the switch).</exception>
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

        var connectionString = options.EffectiveConnectionString(protector);
        if (connectionString.Length == 0)
        {
            connectionString = null;
        }

        return options.ParsedProvider switch
        {
            // Both providers retry transient failures, which is safe because the audited context (E6.4) owns the
            // execution-strategy wrap. PostgreSQL batches are capped at 100 rows, where AuditTrail's own benchmarks
            // stop paying off.
            DatabaseProvider.SqlServer => builder.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(SqlServerMigrationsAssembly).MigrationsHistoryTable(HistoryTable, HistorySchema).EnableRetryOnFailure()),
            DatabaseProvider.PostgreSql => builder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(PostgreSqlMigrationsAssembly).MigrationsHistoryTable(HistoryTable, HistorySchema).EnableRetryOnFailure().MaxBatchSize(WmsAuditing.PostgreSqlMaxBatchSize)),
            _ => throw new InvalidOperationException($"{DatabaseOptions.SectionName}:Provider '{options.Provider}' cannot host a database context."),
        };
    }
}
