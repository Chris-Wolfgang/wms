// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E2.1 on the real API host: an unknown provider fails startup with a message naming the setting, and a
/// configured provider whose server is unreachable still answers the schema endpoint with no current version.
/// </summary>
public sealed class DatabaseStartupTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;



    public DatabaseStartupTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public void An_unknown_provider_fails_startup_with_a_clear_message()
    {
        using var host = _factory.WithWebHostBuilder(builder => builder.UseSetting("Wms:Database:Provider", "Oracle"));

        var exception = Assert.Throws<OptionsValidationException>(() => host.CreateClient());

        Assert.Contains("Wms:Database:Provider must be one of SqlServer, PostgreSql or None; got 'Oracle'.", exception.Message, StringComparison.Ordinal);
    }



    [Fact]
    public void A_provider_without_a_connection_string_fails_startup()
    {
        using var host = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Wms:Database:Provider", "PostgreSql")
            .UseSetting("Wms:Database:ConnectionString", ""));

        var exception = Assert.Throws<OptionsValidationException>(() => host.CreateClient());

        Assert.Contains("Wms:Database:ConnectionString is required", exception.Message, StringComparison.Ordinal);
    }



    [Fact]
    public async Task With_a_configured_but_unreachable_SqlServer_the_schema_endpoint_reports_no_current_version()
    {
        using var host = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Wms:Database:Provider", "SqlServer")
            .UseSetting("Wms:Database:ConnectionString", "Server=127.0.0.1,1;Database=wms;User Id=wms;Password=x;Encrypt=False;Connect Timeout=1;Connect Retry Count=0"));
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v0/system/schema", UriKind.Relative));
        using var status = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, status.RootElement.GetProperty("current").ValueKind);
        Assert.False(status.RootElement.GetProperty("upToDate").GetBoolean());
    }
}
