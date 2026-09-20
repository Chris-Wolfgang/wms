// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Schema;

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

        services.AddDbContext<WmsDbContext>((provider, builder) => Configure(builder, provider.GetRequiredService<IOptions<DatabaseOptions>>().Value));
        services.RemoveAll<ISchemaVersionSource>();
        services.AddScoped<ISchemaVersionSource, MigrationsSchemaVersionSource>();
        services.AddScoped<MigrationRunner>();
        services.AddHostedService<SchemaStartupCheck>();   // E4.4: refuse to start on a schema that is behind or ahead
        return services;
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
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        var connectionString = options.EffectiveConnectionString();
        if (connectionString.Length == 0)
        {
            connectionString = null;
        }

        return options.ParsedProvider switch
        {
            DatabaseProvider.SqlServer => builder.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(SqlServerMigrationsAssembly).MigrationsHistoryTable(HistoryTable, HistorySchema)),
            DatabaseProvider.PostgreSql => builder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(PostgreSqlMigrationsAssembly).MigrationsHistoryTable(HistoryTable, HistorySchema)),
            _ => throw new InvalidOperationException($"{DatabaseOptions.SectionName}:Provider '{options.Provider}' cannot host a database context."),
        };
    }
}
