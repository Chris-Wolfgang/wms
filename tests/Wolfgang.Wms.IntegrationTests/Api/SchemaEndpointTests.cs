// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E82.5 on the real API host: the schema endpoint is served under the versioned root, read-only, and
/// answers before any database exists.
/// </summary>
public sealed class SchemaEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;



    public SchemaEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task Schema_status_is_served_as_camelCase_json_with_no_database_configured()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v0/system/schema", UriKind.Relative));
        using var status = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(JsonValueKind.Null, status.RootElement.GetProperty("current").ValueKind);
        Assert.Equal(JsonValueKind.Null, status.RootElement.GetProperty("expected").ValueKind);
        Assert.False(status.RootElement.GetProperty("upToDate").GetBoolean());
    }



    [Fact]
    public async Task Schema_endpoint_is_read_only()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync(new Uri("/api/v0/system/schema", UriKind.Relative), new StringContent("{}"));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
