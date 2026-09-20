// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wolfgang.Wms.Core.Hosting;
using Wolfgang.Wms.Core.Jobs;

namespace Wolfgang.Wms.UnitTests.Http;

/// <summary>
/// E12.1/E12.5/E12.6 without a host: the HTTPS rule (HTTPS, loopback, health probes and the lab override
/// pass; plain HTTP from the network does not), the probe body, the always-leading lock, and the guards.
/// </summary>
public sealed class HostingTests
{
    [Theory]
    [InlineData(true, "10.0.0.5", "/api/v0/x", false, true)]
    [InlineData(false, "10.0.0.5", "/api/v0/x", false, false)]
    [InlineData(false, "10.0.0.5", "/health/ready", false, true)]
    [InlineData(false, "10.0.0.5", "/api/v0/x", true, true)]
    [InlineData(false, "127.0.0.1", "/api/v0/x", false, true)]
    [InlineData(false, "::1", "/api/v0/x", false, true)]
    [InlineData(false, null, "/api/v0/x", false, true)]
    public void Plain_http_is_refused_from_the_network_only(bool https, string? address, string path, bool allowHttp, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.IsHttps = https;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = address is null ? null : IPAddress.Parse(address);

        Assert.Equal(expected, HttpsRequired.IsAllowed(context, allowHttp));
    }



    [Fact]
    public async Task The_middleware_answers_a_400_problem_or_passes()
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var refused = new DefaultHttpContext { RequestServices = services };
        refused.Request.Path = "/api/v0/x";
        refused.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        refused.Response.Body = new MemoryStream();
        var passed = new DefaultHttpContext { RequestServices = services };
        passed.Request.IsHttps = true;
        var reached = false;
        var configuration = new ConfigurationBuilder().Build();
        var app = new ApplicationBuilder(services);
        app.UseWmsHttpsRequired(configuration);
        app.Run(_ => { reached = true; return Task.CompletedTask; });
        var pipeline = app.Build();

        await pipeline(refused);
        await pipeline(passed);

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Response.StatusCode);
        Assert.True(reached);
        Assert.Equal("hosting.https_required", HttpsRequired.HttpsRequiredCode.Code);
        Assert.Throws<ArgumentNullException>(() => HttpsRequired.IsAllowed(null!, allowHttp: false));
        Assert.Throws<ArgumentNullException>(() => HttpsRequired.UseWmsHttpsRequired(null!, configuration));
        Assert.Throws<ArgumentNullException>(() => app.UseWmsHttpsRequired(null!));
    }



    [Fact]
    public async Task The_probe_body_lists_every_check()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
        {
            ["database"] = new HealthReportEntry(HealthStatus.Healthy, "Schema 42.", TimeSpan.FromMilliseconds(3), exception: null, data: null),
            ["broken"] = new HealthReportEntry(HealthStatus.Unhealthy, description: null, TimeSpan.Zero, new InvalidOperationException("boom"), data: null),
        }, TimeSpan.FromMilliseconds(5));

        await WmsHealth.WriteAsync(context, report);

        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        var checks = json.RootElement.GetProperty("checks").EnumerateArray().ToList();
        Assert.Equal("Unhealthy", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(["database", "broken"], checks.Select(c => c.GetProperty("name").GetString()));
        Assert.Equal(["Schema 42.", "boom"], checks.Select(c => c.GetProperty("description").GetString()));
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        await Assert.ThrowsAsync<ArgumentNullException>(() => WmsHealth.WriteAsync(null!, report));
        await Assert.ThrowsAsync<ArgumentNullException>(() => WmsHealth.WriteAsync(context, null!));
        Assert.Throws<ArgumentNullException>(() => WmsHealth.AddWmsHealth(null!));
        Assert.Throws<ArgumentNullException>(() => WmsHealth.MapWmsHealth(null!));
    }



    [Fact]
    public async Task A_host_without_a_database_always_leads()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWmsHealth();
        using var provider = services.BuildServiceProvider();
        var leader = provider.GetRequiredService<ILeaderLock>();

        var lease = await leader.TryAcquireAsync("job", TimeSpan.FromMinutes(1), CancellationToken.None);
        var again = await leader.TryAcquireAsync("job", TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.IsType<NoLeaderLock>(leader);
        Assert.NotNull(lease);
        Assert.NotNull(again);
        Assert.Equal("job", lease.Name);
        Assert.False(lease.Lost.IsCancellationRequested);
        await lease.DisposeAsync();
        await again!.DisposeAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => leader.TryAcquireAsync(" ", TimeSpan.FromMinutes(1), CancellationToken.None));
    }
}
