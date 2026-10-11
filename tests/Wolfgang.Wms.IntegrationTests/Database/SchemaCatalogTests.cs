// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Infrastructure.Database.Conventions;
using Wolfgang.Wms.IntegrationTests.Database.TestModels;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E3.1–E3.4 as the DBA sees them: a model built with <see cref="ModelConventions"/> is created on each engine
/// and the provider catalogs (<c>INFORMATION_SCHEMA</c>, <c>sys</c>, <c>pg_catalog</c>) are read back, so a
/// provider's own DDL cannot put a table in the wrong schema or emit a different name than the model holds.
/// </summary>
public sealed class SchemaCatalogTests
{
    private const string OurSchemas = "TABLE_SCHEMA IN ('layout', 'picking')";

    private static readonly string[] Tables =
    [
        "layout|zone_group",
        "picking|container",
        "picking|container_line",
    ];

    private static readonly string[] Columns =
    [
        "container_line|container_id",
        "container_line|id",
        "container_line|qty",
        "container|barcode",
        "container|created_at",
        "container|id",
        "container|requested_qty",
        "container|ship_to_street",
        "container|zone_group_id",
        "zone_group|id",
        "zone_group|name",
    ];

    private static readonly string[] Constraints =
    [
        "FOREIGN KEY|fk_container_line_container_id",
        "FOREIGN KEY|fk_container_zone_group_id",
        "PRIMARY KEY|pk_container",
        "PRIMARY KEY|pk_container_line",
        "PRIMARY KEY|pk_zone_group",
    ];

    private static readonly string[] Indexes =
    [
        "layout|pk_zone_group",
        "picking|ix_container_zone_group_id",
        "picking|pk_container",
        "picking|pk_container_line",
        "picking|ux_container_barcode",
    ];

    private static readonly string[] Identities =
    [
        "container_line|id",
        "container|id",
        "zone_group|id",
    ];



    [SqlServerFact]
    public async Task SqlServer_catalog_matches_the_conventions()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();
        var connectionString = database.ConnectionString;   // a database of its own on either path
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await AssertCatalogAsync
        (
            options,
            "SELECT s.name, i.name FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE i.name IS NOT NULL",
            $"SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE {OurSchemas} AND COLUMNPROPERTY(OBJECT_ID(QUOTENAME(TABLE_SCHEMA) + '.' + QUOTENAME(TABLE_NAME)), COLUMN_NAME, 'IsIdentity') = 1",
            ["fk_container_line_container_id|NO ACTION", "fk_container_zone_group_id|NO ACTION"],
            ["container_line|qty|decimal|9|3|", "container|created_at|datetime2|||3", "container|requested_qty|decimal|9|3|"]
        );
    }



    [DockerFact]
    public async Task PostgreSql_catalog_matches_the_conventions()
    {
        await using var container = new PostgreSqlBuilder("postgres:16")
            .Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        await AssertCatalogAsync
        (
            options,
            "SELECT schemaname, indexname FROM pg_catalog.pg_indexes WHERE schemaname IN ('layout', 'picking')",
            $"SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE {OurSchemas} AND IS_IDENTITY = 'YES'",
            ["fk_container_line_container_id|NO ACTION", "fk_container_zone_group_id|RESTRICT"],
            ["container_line|qty|numeric|9|3|", "container|created_at|timestamp with time zone|||3", "container|requested_qty|numeric|9|3|"]
        );
    }



    private static async Task AssertCatalogAsync(DbContextOptions<CatalogDbContext> options, string indexSql, string identitySql, string[] deleteRules, string[] types)
    {
        await using var context = new CatalogDbContext(options);
        Assert.Empty(ModelConventions.Verify(context.Model));
        Assert.True(await context.Database.EnsureCreatedAsync());
        await context.Database.OpenConnectionAsync();

        var tables = await QueryAsync(context, $"SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE {OurSchemas} OR TABLE_SCHEMA IN ('dbo', 'public')");
        var columns = await QueryAsync(context, $"SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE {OurSchemas}");
        var constraints = await QueryAsync(context, $"SELECT CONSTRAINT_TYPE, CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE {OurSchemas} AND CONSTRAINT_TYPE IN ('PRIMARY KEY', 'FOREIGN KEY', 'UNIQUE')");
        var rules = await QueryAsync(context, "SELECT CONSTRAINT_NAME, DELETE_RULE FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA IN ('layout', 'picking')");
        var indexes = await QueryAsync(context, indexSql);
        var identities = await QueryAsync(context, identitySql);
        var columnTypes = await QueryAsync(context, $"SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, NUMERIC_PRECISION, NUMERIC_SCALE, DATETIME_PRECISION FROM INFORMATION_SCHEMA.COLUMNS WHERE {OurSchemas} AND COLUMN_NAME IN ('qty', 'requested_qty', 'created_at')");

        // Nothing in dbo/public; every table in its module schema.
        Assert.Equal(Tables, tables);
        Assert.Equal(Columns, columns);
        Assert.Equal(Constraints, constraints);
        Assert.Equal(Indexes, indexes);
        Assert.Equal(Identities, identities);
        Assert.Equal(deleteRules, rules);
        Assert.Equal(types, columnTypes);
        Assert.All
        (
            columns.Concat(constraints).Concat(indexes).SelectMany(row => row.Split('|')).Where(name => name.Length > 0 && !name.Contains(' ', StringComparison.Ordinal)),
            name => Assert.True(SnakeCase.Is(name), name)
        );
    }



    private static async Task<List<string>> QueryAsync(DbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(string.Join('|', values.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture))));
        }

        return rows.Order(StringComparer.Ordinal).ToList();
    }
}
