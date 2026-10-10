// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.IntegrationTests.Database;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E2.1 through the database module as <c>Program</c> composes it, started directly (<see cref="DatabaseHost"/>):
/// no provider starts without a database, an unknown provider fails startup with a message naming the setting,
/// a provider without a connection string fails startup, and a configured provider whose server is unreachable
/// refuses to start.
/// </summary>
public sealed class DatabaseStartupTests
{
    [Fact]
    public async Task Without_a_provider_the_host_starts_with_no_database()
    {
        await using var app = await DatabaseHost.StartAsync("None", string.Empty, trustServerCertificate: false);

        Assert.Equal(DatabaseProvider.None, app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value.ParsedProvider);
    }



    [Fact]
    public async Task An_unknown_provider_fails_startup_with_a_clear_message()
    {
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => DatabaseHost.StartAsync("Oracle", string.Empty, trustServerCertificate: false));

        Assert.Contains("Wms:Database:Provider must be one of None, SqlServer or PostgreSql; got 'Oracle'.", exception.Message, StringComparison.Ordinal);
    }



    [Fact]
    public async Task A_provider_without_a_connection_string_fails_startup()
    {
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => DatabaseHost.StartAsync("PostgreSql", string.Empty, trustServerCertificate: false));

        Assert.Contains("Wms:Database:ConnectionString is required", exception.Message, StringComparison.Ordinal);
    }



    [Fact]
    public async Task With_a_configured_but_unreachable_SqlServer_the_host_refuses_to_start()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseHost.StartAsync("SqlServer", "Server=127.0.0.1,1;Database=wms;User Id=wms;Password=x;Encrypt=False;Connect Timeout=1;Connect Retry Count=0", trustServerCertificate: false));

        Assert.Contains("cannot be reached", exception.Message, StringComparison.Ordinal);
    }
}
