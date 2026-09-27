// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Modules;

namespace Wolfgang.Wms.IntegrationTests.Modules;

/// <summary>
/// E1.10: a module registered through <c>AddWmsModule</c> has its endpoints mapped by the host's
/// <c>MapWmsModules()</c>; nothing is discovered by scanning.
/// </summary>
public sealed class ModuleEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;



    public ModuleEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task A_registered_modules_endpoint_is_served_by_the_host()
    {
        using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddWmsModule
            (
                ModuleDescriptor
                    .Create("SampleModule")
                    .WithEndpoints(app => app.MapGet("/api/sample/ping", () => "pong"))
            )));
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/sample/ping", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("pong", body);
    }



    [Fact]
    public async Task An_unregistered_route_is_not_served()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/sample/ping", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }



    [Fact]
    public void MapWmsModules_without_AddWmsModules_is_a_configuration_error()
    {
        var app = WebApplication.CreateBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() => ((IEndpointRouteBuilder)app).MapWmsModules());

        Assert.Contains("AddWmsModules", exception.Message, StringComparison.Ordinal);
    }



    [Fact]
    public void MapWmsModules_rejects_a_null_builder()
    {
        Assert.Throws<ArgumentNullException>(() => WmsModuleEndpointRouteBuilderExtensions.MapWmsModules(null!));
    }
}
