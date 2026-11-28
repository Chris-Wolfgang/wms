// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E12.1 on a host without a database: both probes answer 200 JSON, anonymously, outside the versioned
/// root; readiness lists no checks because none is registered without a database.
/// </summary>
public sealed class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;



    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task Both_probes_answer_json_without_a_session()
    {
        using var client = _factory.CreateClient();

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        using var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        using var versioned = await client.GetAsync(new Uri("/api/v0/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, versioned.StatusCode);
        using var liveJson = JsonDocument.Parse(await live.Content.ReadAsStringAsync());
        using var readyJson = JsonDocument.Parse(await ready.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", liveJson.RootElement.GetProperty("status").GetString());
        Assert.Empty(liveJson.RootElement.GetProperty("checks").EnumerateArray());
        Assert.Equal("Healthy", readyJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("no-store", ready.Headers.CacheControl?.ToString());
    }
}
