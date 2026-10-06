// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Devices;
using Wolfgang.Wms.Core.Http;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E82.7 on a throwaway host: a device group requires the app version header; below the site's minimum the
/// answer is 426 with the minimum; without a configured minimum every version passes.
/// </summary>
public sealed class DeviceVersionFilterTests : IAsyncLifetime
{
    private readonly ConfigurableMinimum _minimum = new();
    private WebApplication? _app;
    private HttpClient? _client;



    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddWmsApiVersioning();
        builder.Services.AddWmsProblemDetails();
        builder.Services.AddSingleton<IDeviceVersionPolicy>(_minimum);
        builder.Services.AddWmsDeviceVersioning();
        _app = builder.Build();

        var api = _app.MapWmsApi();
        var device = api.MapGroup("/device").RequireDeviceVersion();
        device.MapGet("/ping", () => "pong");
        api.MapGet("/console/ping", () => "pong");

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



    [Fact]
    public async Task A_missing_header_is_400_with_the_missing_code()
    {
        _minimum.Value = new Version(1, 4, 0);

        using var response = await Send("/api/v0/device/ping", null);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("device.version_missing", problem.RootElement.GetProperty("code").GetString());
    }



    [Fact]
    public async Task An_unparseable_header_is_400_with_the_invalid_code()
    {
        using var response = await Send("/api/v0/device/ping", "latest");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("device.version_invalid", problem.RootElement.GetProperty("code").GetString());
        Assert.Contains("latest", problem.RootElement.GetProperty("title").GetString(), StringComparison.Ordinal);
    }



    [Fact]
    public async Task An_app_below_the_minimum_gets_426_with_the_minimum()
    {
        _minimum.Value = new Version(1, 4, 0);

        using var response = await Send("/api/v0/device/ping", "1.3.9");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
        Assert.Equal("device.version_too_old", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("1.4.0", problem.RootElement.GetProperty(DeviceVersionFilter.MinimumVersionExtension).GetString());
    }



    [Theory]
    [InlineData("1.4.0")]
    [InlineData("1.4.0-alpha.0.3")]
    [InlineData("2.0")]
    public async Task An_app_at_or_above_the_minimum_passes(string header)
    {
        _minimum.Value = new Version(1, 4, 0);

        using var response = await Send("/api/v0/device/ping", header);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("pong", await response.Content.ReadAsStringAsync());
    }



    [Fact]
    public async Task Without_a_configured_minimum_any_version_passes()
    {
        _minimum.Value = null;

        using var response = await Send("/api/v0/device/ping", "0.0.1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }



    [Fact]
    public async Task Endpoints_outside_the_device_group_do_not_require_the_header()
    {
        using var response = await Send("/api/v0/console/ping", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }



    private async Task<HttpResponseMessage> Send(string path, string? version)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (version is not null)
        {
            request.Headers.Add(DeviceVersion.HeaderName, version);
        }

        return await _client!.SendAsync(request);
    }



    private sealed class ConfigurableMinimum : IDeviceVersionPolicy
    {
        public Version? Value { get; set; }

        public Task<Version?> GetMinimumAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Value);
        }
    }
}
