// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Data.SqlClient;
using Npgsql;

namespace Wolfgang.Wms.UnitTests.Migrate;

/// <summary>
/// Connection strings for tests that never connect, built with each provider's builder rather than written as
/// literals: CodeQL's cs/insecure-sql-connection follows any literal without <c>Encrypt=True</c> into the SQL
/// Server connection setup, whichever provider it was meant for.
/// </summary>
internal static class TestConnectionStrings
{
    public static string SqlServer(string server)
    {
        return new SqlConnectionStringBuilder { DataSource = server, Encrypt = SqlConnectionEncryptOption.Mandatory }.ConnectionString;
    }



    public static string PostgreSql(string host)
    {
        return new NpgsqlConnectionStringBuilder { Host = host }.ConnectionString;
    }
}
