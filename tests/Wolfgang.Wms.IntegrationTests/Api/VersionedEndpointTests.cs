// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Wolfgang.Wms.Core.Api;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E82.2 on a throwaway host with one endpoint mapped under the versioned root: the version is a path
/// segment, an unsupported or missing version is refused, and the OpenAPI document lists the endpoint under
/// its concrete path without a <c>version</c> parameter.
/// </summary>
public sealed class VersionedEndpointTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient? _client;



    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddWmsApiVersioning();
        _app = builder.Build();

        var api = _app.MapWmsApi();
        api.MapGet("/ping/{name}", (string name) => $"pong {name}");

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }



    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }



    [Theory]
    [InlineData("/api/v0/ping/a", HttpStatusCode.OK)]
    [InlineData("/api/v9/ping/a", HttpStatusCode.NotFound)]
    [InlineData("/api/ping/a", HttpStatusCode.NotFound)]
    [InlineData("/ping/a", HttpStatusCode.NotFound)]
    public async Task Endpoints_answer_only_under_a_served_version_segment(string path, HttpStatusCode expected)
    {
        using var response = await _client!.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(expected, response.StatusCode);
    }



    [Fact]
    public async Task Responses_report_the_supported_versions()
    {
        using var response = await _client!.GetAsync(new Uri("/api/v0/ping/a", UriKind.Relative));

        Assert.Equal(["0"], response.Headers.GetValues("api-supported-versions"));
    }



    [Fact]
    public async Task OpenApi_document_lists_the_endpoint_under_its_concrete_path_without_a_version_parameter()
    {
        var json = await _client!.GetStringAsync(new Uri("/openapi/v0.json", UriKind.Relative));
        using var document = JsonDocument.Parse(json);

        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty("/api/v0/ping/{name}").GetProperty("get");
        var parameters = operation.GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();

        Assert.Single(paths.EnumerateObject());
        Assert.Equal(["name"], parameters);
        Assert.False(document.RootElement.TryGetProperty("servers", out _));
    }
}
