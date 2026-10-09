// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Npgsql;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// A runtime login for one test database with the rights the API's service account holds in an install
/// (E4.4; <c>docker/db-init.sh</c>: data reader and writer, never schema rights). The host under test connects
/// with it, so a test fails where the API would need a right the service account does not have; the tool and
/// the test keep the test's own (sysadmin) connection. SQL Server role membership covers tables created later;
/// PostgreSQL grants are per schema, so <see cref="GrantDataAsync"/> runs again after <c>wms-migrate</c>.
/// </summary>
internal static class TestLogins
{
    /// <summary>
    /// Creates the login and its user in the database <paramref name="adminConnectionString"/> names, grants it
    /// data rights on what exists now, and returns the connection string the host uses.
    /// </summary>
    public static async Task<string> CreateRuntimeAsync(string provider, string adminConnectionString, CancellationToken cancellationToken = default)
    {
        var password = "Rt!" + Guid.NewGuid().ToString("N") + "aZ9";
        if (IsSqlServer(provider))
        {
            var admin = new SqlConnectionStringBuilder(adminConnectionString);
            var login = RuntimeLoginName(admin.InitialCatalog);
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await ExecuteAsync(connection, string.Format(CultureInfo.InvariantCulture, "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'{0}') CREATE LOGIN [{0}] WITH PASSWORD = N'{1}', CHECK_POLICY = ON", login, password), cancellationToken);
                await ExecuteAsync(connection, string.Format(CultureInfo.InvariantCulture, "IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'{0}') CREATE USER [{0}] FOR LOGIN [{0}]", login), cancellationToken);
            }

            var runtime = new SqlConnectionStringBuilder(adminConnectionString) { IntegratedSecurity = false, UserID = login, Password = password };
            await GrantDataAsync(provider, adminConnectionString, runtime.ConnectionString, cancellationToken);
            return runtime.ConnectionString;
        }

        var adminPg = new NpgsqlConnectionStringBuilder(adminConnectionString);
        var role = RuntimeLoginName(adminPg.Database ?? "wms");
        await using (var connection = new NpgsqlConnection(adminConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await ExecuteAsync(connection, string.Format(CultureInfo.InvariantCulture, "CREATE ROLE \"{0}\" LOGIN PASSWORD '{1}'", role, password), cancellationToken);
            await ExecuteAsync(connection, string.Format(CultureInfo.InvariantCulture, "GRANT CONNECT ON DATABASE \"{0}\" TO \"{1}\"", adminPg.Database, role), cancellationToken);
        }

        var runtimePg = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = role, Password = password };
        await GrantDataAsync(provider, adminConnectionString, runtimePg.ConnectionString, cancellationToken);
        return runtimePg.ConnectionString;
    }



    /// <summary>
    /// Grants the runtime login read and write on every table of the application's schemas (the ones that exist
    /// now): SQL Server through <c>db_datareader</c> / <c>db_datawriter</c>, PostgreSQL per schema.
    /// </summary>
    public static async Task GrantDataAsync(string provider, string adminConnectionString, string runtimeConnectionString, CancellationToken cancellationToken = default)
    {
        if (IsSqlServer(provider))
        {
            await GrantSqlServerAsync(adminConnectionString, new SqlConnectionStringBuilder(runtimeConnectionString).UserID, cancellationToken);
        }
        else
        {
            await GrantPostgreSqlAsync(adminConnectionString, new NpgsqlConnectionStringBuilder(runtimeConnectionString).Username!, cancellationToken);
        }
    }



    private static async Task GrantSqlServerAsync(string adminConnectionString, string login, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(adminConnectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, string.Format(CultureInfo.InvariantCulture, "ALTER ROLE db_datareader ADD MEMBER [{0}]; ALTER ROLE db_datawriter ADD MEMBER [{0}]", login), cancellationToken);
    }



    private static async Task GrantPostgreSqlAsync(string adminConnectionString, string role, CancellationToken cancellationToken)
    {
        await using var pg = new NpgsqlConnection(adminConnectionString);
        await pg.OpenAsync(cancellationToken);
        var schemas = new List<string>();
        await using (var list = pg.CreateCommand())
        {
            list.CommandText = "SELECT nspname FROM pg_catalog.pg_namespace WHERE nspname NOT IN ('pg_catalog', 'information_schema', 'public') AND nspname NOT LIKE 'pg_%'";
            await using var reader = await list.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                schemas.Add(reader.GetString(0));
            }
        }

        foreach (var schema in schemas)
        {
            await ExecuteAsync(pg, string.Format(CultureInfo.InvariantCulture, "GRANT USAGE ON SCHEMA \"{0}\" TO \"{1}\"; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA \"{0}\" TO \"{1}\"", schema, role), cancellationToken);
        }
    }



    /// <summary>
    /// Asserts the runtime login cannot change the schema: a <c>CREATE TABLE</c> in the migrations history
    /// schema is refused by the engine.
    /// </summary>
    public static async Task AssertCannotChangeSchemaAsync(string provider, string runtimeConnectionString)
    {
        await using DbConnection connection = IsSqlServer(provider) ? new SqlConnection(runtimeConnectionString) : new NpgsqlConnection(runtimeConnectionString);
        await connection.OpenAsync();
        var refused = await Assert.ThrowsAnyAsync<DbException>(() => ExecuteAsync(connection, "CREATE TABLE " + DatabaseServiceCollectionExtensions.HistorySchema + ".runtime_login_probe (id int)", CancellationToken.None));

        Assert.Contains("permission", refused.Message, StringComparison.OrdinalIgnoreCase);
    }



    /// <summary>
    /// The login created for the database <paramref name="database"/> (server-scoped on SQL Server, so
    /// <see cref="SqlServerTestDatabase"/> drops it with the database).
    /// </summary>
    public static string RuntimeLoginName(string database)
    {
        return "wms_runtime_" + database;
    }



    private static bool IsSqlServer(string provider)
    {
        return string.Equals(provider, "SqlServer", StringComparison.Ordinal);
    }



    private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
