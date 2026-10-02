// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore.Migrations;

namespace Wolfgang.Wms.Infrastructure.Database.Conventions;

/// <summary>
/// The provider SQL behind <c>row_version</c> (E5.1): one <c>bigint</c> sequence per database, a column
/// default that draws from it on insert (so EF batches, <c>INSERT … SELECT</c>, <c>COPY</c> and
/// <c>SqlBulkCopy</c> are all covered), and an update trigger that overwrites any supplied value (set-based
/// on SQL Server, per row on PostgreSQL) so writes outside EF are covered too. Migrations that create a
/// versioned table call <see cref="AddUpdateTrigger"/>; the sequence itself is part of the model.
/// </summary>
public static class RowVersioning
{
    /// <summary>
    /// Schema of the sequence and the PostgreSQL trigger function.
    /// </summary>
    public const string Schema = "wms";



    /// <summary>
    /// Name of the single sequence every versioned table draws from.
    /// </summary>
    public const string SequenceName = "row_version_seq";



    /// <summary>
    /// The versioned column.
    /// </summary>
    public const string ColumnName = "row_version";



    /// <summary>
    /// How many values a session pre-allocates; gaps are acceptable (E5.3).
    /// </summary>
    public const int SequenceCache = 100;



    /// <summary>
    /// EF provider name of SQL Server.
    /// </summary>
    public const string SqlServer = "Microsoft.EntityFrameworkCore.SqlServer";



    /// <summary>
    /// EF provider name of PostgreSQL.
    /// </summary>
    public const string PostgreSql = "Npgsql.EntityFrameworkCore.PostgreSQL";



    /// <summary>
    /// The column default that assigns the next sequence value on insert.
    /// </summary>
    /// <exception cref="ArgumentException">Unknown provider.</exception>
    public static string DefaultValueSql(string? providerName)
    {
        return providerName switch
        {
            SqlServer => $"NEXT VALUE FOR [{Schema}].[{SequenceName}]",
            PostgreSql => $"nextval('{Schema}.{SequenceName}')",
            _ => throw new ArgumentException($"Unknown provider '{providerName}'.", nameof(providerName)),
        };
    }



    /// <summary>
    /// Name of the update trigger of a table.
    /// </summary>
    public static string TriggerName(string table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        return "trg_" + table + "_row_version";
    }



    /// <summary>
    /// Adds the update trigger for a versioned table (and, on PostgreSQL, the shared trigger function).
    /// Call from the migration that creates the table.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="migrationBuilder"/> is null.</exception>
    public static void AddUpdateTrigger(MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(UpdateTriggerSql(migrationBuilder.ActiveProvider, schema, table));
    }



    /// <summary>
    /// Drops the update trigger of a versioned table. Call from the migration's <c>Down</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="migrationBuilder"/> is null.</exception>
    public static void DropUpdateTrigger(MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(DropTriggerSql(migrationBuilder.ActiveProvider, schema, table));
    }



    /// <summary>
    /// The SQL that creates the update trigger for a table on a provider.
    /// </summary>
    /// <exception cref="ArgumentException">Unknown provider or blank names.</exception>
    public static string UpdateTriggerSql(string? providerName, string schema, string table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        var trigger = TriggerName(table);
        return providerName switch
        {
            SqlServer => $"""
                CREATE OR ALTER TRIGGER [{schema}].[{trigger}] ON [{schema}].[{table}] AFTER UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    UPDATE t SET [{ColumnName}] = NEXT VALUE FOR [{Schema}].[{SequenceName}]
                    FROM [{schema}].[{table}] t INNER JOIN inserted i ON t.[id] = i.[id];
                END
                """,
            PostgreSql => $"""
                CREATE OR REPLACE FUNCTION {Schema}.set_row_version() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    NEW.{ColumnName} := nextval('{Schema}.{SequenceName}');
                    RETURN NEW;
                END $$;
                DROP TRIGGER IF EXISTS {trigger} ON {schema}.{table};
                CREATE TRIGGER {trigger} BEFORE UPDATE ON {schema}.{table} FOR EACH ROW EXECUTE FUNCTION {Schema}.set_row_version();
                """,
            _ => throw new ArgumentException($"Unknown provider '{providerName}'.", nameof(providerName)),
        };
    }



    /// <summary>
    /// The SQL that drops the update trigger of a table on a provider.
    /// </summary>
    /// <exception cref="ArgumentException">Unknown provider or blank names.</exception>
    public static string DropTriggerSql(string? providerName, string schema, string table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        var trigger = TriggerName(table);
        return providerName switch
        {
            SqlServer => $"DROP TRIGGER IF EXISTS [{schema}].[{trigger}];",
            PostgreSql => $"DROP TRIGGER IF EXISTS {trigger} ON {schema}.{table};",
            _ => throw new ArgumentException($"Unknown provider '{providerName}'.", nameof(providerName)),
        };
    }
}
