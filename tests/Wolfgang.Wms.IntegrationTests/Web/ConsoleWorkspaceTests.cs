// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Wolfgang.Wms.Web.Components;

namespace Wolfgang.Wms.IntegrationTests.Web;

/// <summary>
/// E82.4 through the real console host: every workspace is routed from its own assembly, the entry gate
/// renders the body for a licensed workspace and a not-licensed page for a paid one, and <c>/</c> offers the
/// chooser. Server render mode pre-renders the markup, so plain HTTP requests see it.
/// </summary>
public sealed class ConsoleWorkspaceTests : IClassFixture<WebApplicationFactory<App>>
{
    private readonly WebApplicationFactory<App> _factory;



    public ConsoleWorkspaceTests(WebApplicationFactory<App> factory)
    {
        _factory = factory;
    }



    [Theory]
    [InlineData("/configure", "configure", "Configure")]
    [InlineData("/supervise", "supervise", "Supervise")]
    [InlineData("/resolve", "resolve", "Resolve")]
    [InlineData("/report", "report", "Report")]
    public async Task A_free_tier_workspace_renders_its_body_inside_the_workspace_chrome(string route, string name, string title)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri(route, UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"data-workspace=\"{name}\"", html, StringComparison.Ordinal);
        Assert.Contains($"<h1>{title}</h1>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"workspace-nav\"", html, StringComparison.Ordinal);
    }



    [Fact]
    public async Task A_paid_workspace_renders_the_not_licensed_page_instead_of_its_body()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/insights", UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Insights is not licensed", html, StringComparison.Ordinal);
        Assert.Contains("workspace.insights", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Findings and recommendations", html, StringComparison.Ordinal);
    }



    [Fact]
    public async Task The_root_offers_the_enterable_workspaces()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("class=\"workspace-chooser\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/configure\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/report\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/insights\"", html, StringComparison.Ordinal);
    }



    [Fact]
    public async Task The_workspace_navigation_lists_only_enterable_workspaces()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync(new Uri("/configure", UriKind.Relative));

        Assert.Contains("href=\"/supervise\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/insights\"", html, StringComparison.Ordinal);
    }
}
