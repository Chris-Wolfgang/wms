// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolfgang.Wms.Web.Components;
using Wolfgang.Wms.Web.Shared;

namespace Wolfgang.Wms.IntegrationTests.Web;

/// <summary>
/// The console host's own pages and pipeline (E82.4), plus the entry-gate branches that need an access
/// answer other than the free tier's: a single enterable workspace lands the user in it, a workspace the
/// user's roles do not grant renders the not-permitted page, and a licensed paid workspace renders its body.
/// </summary>
public sealed class ConsoleHostTests : IClassFixture<WebApplicationFactory<App>>
{
    private readonly WebApplicationFactory<App> _factory;



    public ConsoleHostTests(WebApplicationFactory<App> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task The_error_page_renders()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/Error", UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Error", html, StringComparison.Ordinal);
    }



    [Fact]
    public async Task An_unknown_route_renders_the_not_found_page_with_404()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/no-such-workspace", UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Not Found", html, StringComparison.OrdinalIgnoreCase);
    }



    [Fact]
    public async Task The_production_pipeline_serves_the_console_with_hsts_and_the_exception_handler()
    {
        using var production = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = production.CreateClient();

        using var response = await client.GetAsync(new Uri("/configure", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }



    [Fact]
    public async Task A_user_with_one_enterable_workspace_lands_in_it()
    {
        using var host = WithAccess(_factory, w => w == Workspaces.Report ? WorkspaceAccessResult.Allowed : WorkspaceAccessResult.NotPermitted);
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/report", response.RequestMessage?.RequestUri?.AbsolutePath);
        Assert.Contains("data-workspace=\"report\"", html, StringComparison.Ordinal);
    }



    [Fact]
    public async Task A_user_without_the_permission_sees_the_not_permitted_page()
    {
        using var host = WithAccess(_factory, _ => WorkspaceAccessResult.NotPermitted);
        using var client = host.CreateClient();

        var configure = await client.GetStringAsync(new Uri("/configure", UriKind.Relative));
        var root = await client.GetStringAsync(new Uri("/", UriKind.Relative));

        Assert.Contains("Configure is not available to you", configure, StringComparison.Ordinal);
        Assert.Contains("workspace.configure.enter", configure, StringComparison.Ordinal);
        Assert.Contains("None of the console workspaces is available to you", root, StringComparison.Ordinal);
    }



    [Fact]
    public async Task A_licensed_paid_workspace_renders_its_body()
    {
        using var host = WithAccess(_factory, _ => WorkspaceAccessResult.Allowed);
        using var client = host.CreateClient();

        var html = await client.GetStringAsync(new Uri("/insights", UriKind.Relative));

        Assert.Contains("<h1>Insights</h1>", html, StringComparison.Ordinal);
        Assert.Contains("Findings and recommendations", html, StringComparison.Ordinal);
    }



    private static WebApplicationFactory<App> WithAccess(WebApplicationFactory<App> factory, Func<Workspace, WorkspaceAccessResult> answer)
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IWorkspaceAccess>(new FakeWorkspaceAccess(answer)))));
    }



    private sealed class FakeWorkspaceAccess : IWorkspaceAccess
    {
        private readonly Func<Workspace, WorkspaceAccessResult> _answer;



        public FakeWorkspaceAccess(Func<Workspace, WorkspaceAccessResult> answer)
        {
            _answer = answer;
        }



        public Task<WorkspaceAccessResult> CheckAsync(Workspace workspace, CancellationToken cancellationToken)
        {
            return Task.FromResult(_answer(workspace));
        }
    }
}
