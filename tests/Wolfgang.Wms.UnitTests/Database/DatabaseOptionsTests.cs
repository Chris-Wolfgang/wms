// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Schema;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.UnitTests.Database;

public sealed class DatabaseOptionsTests
{
    [Theory]
    [InlineData("SqlServer", DatabaseProvider.SqlServer)]
    [InlineData("sqlserver", DatabaseProvider.SqlServer)]
    [InlineData("PostgreSql", DatabaseProvider.PostgreSql)]
    [InlineData("POSTGRESQL", DatabaseProvider.PostgreSql)]
    [InlineData("None", DatabaseProvider.None)]
    public void Provider_names_parse_case_insensitively(string text, DatabaseProvider expected)
    {
        Assert.Equal(expected, new DatabaseOptions { Provider = text }.ParsedProvider);
    }



    [Theory]
    [InlineData("Oracle")]
    [InlineData("")]
    [InlineData("7")]
    public void An_unknown_provider_fails_validation_naming_the_setting_and_the_accepted_values(string text)
    {
        var errors = new DatabaseOptions { Provider = text }.Validate();

        var error = Assert.Single(errors);
        Assert.Equal($"Wms:Database:Provider must be one of SqlServer, PostgreSql or None; got '{text}'.", error);
    }



    [Fact]
    public void A_real_provider_needs_a_connection_string_and_TrustServerCertificate_is_SqlServer_only()
    {
        Assert.Equal
        (
            ["Wms:Database:ConnectionString is required when Wms:Database:Provider is SqlServer."],
            new DatabaseOptions { Provider = "SqlServer" }.Validate()
        );
        Assert.Equal
        (
            ["Wms:Database:TrustServerCertificate applies to SqlServer only; remove it for PostgreSql."],
            new DatabaseOptions { Provider = "PostgreSql", ConnectionString = "Host=db", TrustServerCertificate = true }.Validate()
        );
        Assert.Empty(new DatabaseOptions { Provider = "None" }.Validate());
        Assert.Empty(new DatabaseOptions { Provider = "SqlServer", ConnectionString = "Server=db", TrustServerCertificate = true }.Validate());
    }



    [Fact]
    public void TrustServerCertificate_is_applied_to_the_SqlServer_connection_string_only()
    {
        var sqlServer = new DatabaseOptions { Provider = "SqlServer", ConnectionString = "Server=db;Database=wms", TrustServerCertificate = true };
        var postgres = new DatabaseOptions { Provider = "PostgreSql", ConnectionString = "Host=db;Database=wms" };

        Assert.True(new SqlConnectionStringBuilder(sqlServer.EffectiveConnectionString()).TrustServerCertificate);
        Assert.Equal("Host=db;Database=wms", postgres.EffectiveConnectionString());
        Assert.Equal(string.Empty, new DatabaseOptions().EffectiveConnectionString());
    }



    [Fact]
    public void The_validator_reports_every_error_and_succeeds_when_valid()
    {
        var validator = new DatabaseOptionsValidator();

        var failed = validator.Validate(null, new DatabaseOptions { Provider = "SqlServer" });
        var ok = validator.Validate(null, new DatabaseOptions { Provider = "None" });

        Assert.True(failed.Failed);
        Assert.Contains("Wms:Database:ConnectionString", failed.FailureMessage, StringComparison.Ordinal);
        Assert.True(ok.Succeeded);
        Assert.Throws<ArgumentNullException>(() => validator.Validate(null, null!));
    }



    [Theory]
    [InlineData("SqlServer", "Server=localhost;Database=wms", "Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("PostgreSql", "Host=localhost;Database=wms", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void AddWmsDatabase_registers_the_context_on_the_configured_provider_and_the_migrations_schema_source(string provider, string connectionString, string providerName)
    {
        using var services = Build(provider, connectionString);
        using var scope = services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var source = scope.ServiceProvider.GetRequiredService<ISchemaVersionSource>();

        Assert.Equal(providerName, context.Database.ProviderName);
        Assert.IsType<MigrationsSchemaVersionSource>(source);
    }



    [Fact]
    public void AddWmsDatabase_with_None_registers_no_context_and_keeps_the_bootstrap_schema_source()
    {
        using var services = Build("None", null);

        Assert.Null(services.GetService<WmsDbContext>());
        Assert.IsType<NotInstalledSchemaVersionSource>(services.GetRequiredService<ISchemaVersionSource>());
    }



    [Fact]
    public void AddWmsDatabase_with_an_unknown_provider_fails_when_the_options_are_validated()
    {
        using var services = Build("Oracle", "x");

        var exception = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<DatabaseOptions>>().Value);

        Assert.Contains("got 'Oracle'", exception.Message, StringComparison.Ordinal);
    }



    [Fact]
    public void Configure_refuses_a_provider_that_cannot_host_a_context_and_null_arguments()
    {
        var builder = new DbContextOptionsBuilder<WmsDbContext>();

        Assert.Throws<InvalidOperationException>(() => DatabaseServiceCollectionExtensions.Configure(builder, new DatabaseOptions { Provider = "None" }));
        Assert.Throws<ArgumentNullException>(() => DatabaseServiceCollectionExtensions.Configure(null!, new DatabaseOptions()));
        Assert.Throws<ArgumentNullException>(() => DatabaseServiceCollectionExtensions.Configure(builder, null!));
        Assert.Throws<ArgumentNullException>(() => DatabaseServiceCollectionExtensions.AddWmsDatabase(null!, new ConfigurationBuilder().Build()));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddWmsDatabase(null!));
        Assert.Throws<ArgumentNullException>(() => new MigrationsSchemaVersionSource(null!));
    }



    private static ServiceProvider Build(string provider, string? connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Wms:Database:Provider"] = provider,
                ["Wms:Database:ConnectionString"] = connectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWmsModules();
        services.AddWmsSchemaModule();
        services.AddWmsDatabase(configuration);
        return services.BuildServiceProvider();
    }
}
