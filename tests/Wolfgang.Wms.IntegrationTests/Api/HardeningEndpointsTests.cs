// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Wolfgang.Wms.Core.Hosting;
using Wolfgang.Wms.Web.Components;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E10.6 on real hosts: the forwarded scheme is honoured only behind a proxy; without an allowed origin a
/// cross-origin request gets no CORS grant from the API; the console sends its security headers.
/// </summary>
public sealed class HardeningEndpointsTests : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<WebApplicationFactory<App>>
{
    private readonly WebApplicationFactory<Program> _api;
    private readonly WebApplicationFactory<App> _console;



    public HardeningEndpointsTests(WebApplicationFactory<Program> api, WebApplicationFactory<App> console)
    {
        _api = api;
        _console = console;
    }



    [Theory]
    [InlineData(true, "https")]
    [InlineData(false, "http")]
    public async Task The_forwarded_scheme_is_honoured_only_behind_a_proxy(bool behindProxy, string expectedScheme)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [HostingOptions.BehindProxyKey] = behindProxy ? "true" : "false" });
        builder.Services.AddWmsForwardedHeaders(builder.Configuration);
        await using var app = builder.Build();
        app.UseWmsForwardedHeaders();
        app.MapGet("/scheme", (HttpRequest request) => request.Scheme + " " + (request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "none"));
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/scheme", UriKind.Relative));
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "203.0.113.9");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.StartsWith(expectedScheme + " ", body, StringComparison.Ordinal);
        Assert.Equal(behindProxy, body.EndsWith("203.0.113.9", StringComparison.Ordinal));
    }



    [Fact]
    public async Task Without_an_allowed_origin_the_api_grants_no_cross_origin_access()
    {
        using var client = _api.CreateClient();

        using var preflight = new HttpRequestMessage(HttpMethod.Options, new Uri("/api/v0/auth/me", UriKind.Relative));
        preflight.Headers.Add("Origin", "https://apps.example.com");
        preflight.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await client.SendAsync(preflight);

        Assert.DoesNotContain("Access-Control-Allow-Origin", response.Headers.Select(h => h.Key));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }



    [Fact]
    public async Task The_console_sends_its_security_headers()
    {
        using var client = _console.CreateClient();

        using var response = await client.GetAsync(new Uri("/configure", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }
}
