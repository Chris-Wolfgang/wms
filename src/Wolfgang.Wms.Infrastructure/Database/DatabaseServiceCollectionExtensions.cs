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

        var options = new DatabaseOptions();
        configuration.GetSection(DatabaseOptions.SectionName).Bind(options);
        if (options.ParsedProvider is null or DatabaseProvider.None)
        {
            return services;
        }

        services.AddDbContext<WmsDbContext>((provider, builder) => Configure(builder, provider.GetRequiredService<IOptions<DatabaseOptions>>().Value));
        services.RemoveAll<ISchemaVersionSource>();
        services.AddScoped<ISchemaVersionSource, MigrationsSchemaVersionSource>();
        return services;
    }



    /// <summary>
    /// Points the context at the configured provider. Called per context; the options are already validated.
    /// </summary>
    /// <exception cref="InvalidOperationException">The provider is not one a context can run on.</exception>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        return options.ParsedProvider switch
        {
            DatabaseProvider.SqlServer => builder.UseSqlServer(options.EffectiveConnectionString()),
            DatabaseProvider.PostgreSql => builder.UseNpgsql(options.EffectiveConnectionString()),
            _ => throw new InvalidOperationException($"{DatabaseOptions.SectionName}:Provider '{options.Provider}' cannot host a database context."),
        };
    }
}
