// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text;
using Microsoft.Extensions.Configuration;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;

namespace Wolfgang.Wms.UnitTests.Configuration;

/// <summary>
/// E8.4: environment variables (<c>Wms__Database__ConnectionString</c>, <c>Wms__DataProtection__KeyRingPath</c>)
/// override the appsettings values, the way the hosts' configuration is layered, so container secret stores
/// can supply them.
/// </summary>
public sealed class EnvironmentOverridesTests
{
    [Fact]
    public void Environment_variables_win_over_appsettings_for_the_connection_string_and_the_key_ring_path()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{ "Wms": { "Database": { "Provider": "PostgreSql", "ConnectionString": "Host=file" }, "DataProtection": { "KeyRingPath": "/from-file" } } }"""));
        var database = new DatabaseOptions();
        var keyRing = new KeyRingOptions();
        Environment.SetEnvironmentVariable("WMSTEST_Wms__Database__ConnectionString", "Host=env");
        Environment.SetEnvironmentVariable("WMSTEST_Wms__DataProtection__KeyRingPath", "/from-env");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonStream(stream)
                .AddEnvironmentVariables("WMSTEST_")   // the hosts add environment variables after appsettings*.json
                .Build();
            configuration.GetSection(DatabaseOptions.SectionName).Bind(database);
            configuration.GetSection(KeyRingOptions.SectionName).Bind(keyRing);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WMSTEST_Wms__Database__ConnectionString", null);
            Environment.SetEnvironmentVariable("WMSTEST_Wms__DataProtection__KeyRingPath", null);
        }

        Assert.Equal("Host=env", database.ConnectionString);
        Assert.Equal("PostgreSql", database.Provider);
        Assert.Equal("/from-env", keyRing.KeyRingPath);
    }
}
