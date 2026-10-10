// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Data.SqlClient;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// The throw-away database factory itself (E13.1): what it starts, it also cleans up when it fails.
/// </summary>
public sealed class SqlServerTestDatabaseTests
{
    [SqlServerFact]
    public async Task StartAsync_leaves_nothing_running_when_the_database_cannot_be_created()
    {
        // An unbracketable name makes CREATE DATABASE fail after the engine is up: on the container path that is
        // the moment a started container has no owner yet, and the factory must dispose it before rethrowing.
        var exception = await Assert.ThrowsAsync<SqlException>(() => SqlServerTestDatabase.StartAsync(name: "x]y"));

        Assert.Contains("Incorrect syntax", exception.Message, StringComparison.Ordinal);
    }
}
