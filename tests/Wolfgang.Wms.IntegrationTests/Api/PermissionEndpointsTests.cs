// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Authorization;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E10.1 on the real API host: every endpoint declares a permission or explicit anonymity; a request without
/// a session is 401, one without the permission 403, one with it (everywhere or at the request's site)
/// runs; the catalog lists the modules' and the console's permissions.
/// </summary>
public sealed class PermissionEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;



    public PermissionEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public void Every_endpoint_declares_a_permission_or_says_it_is_anonymous()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        var undeclared = endpoints
            .Where(e => e.Metadata.GetMetadata<PermissionMetadata>() is null && e.Metadata.GetMetadata<IAuthorizeData>() is null && e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Select(e => e.RoutePattern.RawText)
            .Where(route => route is not null && !route.StartsWith("/openapi", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.Empty(undeclared);
        Assert.Contains(endpoints, e => string.Equals(e.Metadata.GetMetadata<PermissionMetadata>()?.Permission.Name, "settings.write", StringComparison.Ordinal));
    }



    [Fact]
    public async Task Requests_are_401_without_a_session_403_without_the_permission_and_run_with_it()
    {
        using var host = _factory.WithTestAuth();
        using var client = host.CreateClient();

        using var anonymous = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/settings/registry", grants: null));
        using var wrongPermission = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/settings/registry", "settings.write@organization"));
        using var siteOnly = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/settings/registry", "settings.read@site:3"));
        using var siteRequest = await client.SendAsync(WithSite(TestAuth.As(HttpMethod.Get, "/api/v0/settings/registry", "settings.read@site:3"), 3));
        using var otherSite = await client.SendAsync(WithSite(TestAuth.As(HttpMethod.Get, "/api/v0/settings/registry", "settings.read@site:3"), 4));
        using var everywhere = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/settings/registry", "settings.read@organization"));
        using var wildcard = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/settings/registry", "*@organization"));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("auth.not_signed_in", await CodeAsync(anonymous));
        Assert.Equal(HttpStatusCode.Forbidden, wrongPermission.StatusCode);
        Assert.Equal("auth.forbidden", await CodeAsync(wrongPermission));
        Assert.Equal(HttpStatusCode.Forbidden, siteOnly.StatusCode);   // an organisation-wide request needs an organisation grant
        Assert.Equal(HttpStatusCode.OK, siteRequest.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherSite.StatusCode);   // site A never grants site B
        Assert.Equal(HttpStatusCode.OK, everywhere.StatusCode);
        Assert.Equal(HttpStatusCode.OK, wildcard.StatusCode);
    }



    [Fact]
    public async Task The_catalog_lists_module_and_console_permissions_for_any_signed_in_user()
    {
        using var host = _factory.WithTestAuth();
        using var client = host.CreateClient();

        using var anonymous = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/auth/permissions", grants: null));
        using var signedIn = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/auth/permissions", "none@organization"));   // any session, no particular permission
        using var me = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/auth/me", "settings.read@organization"));
        using var catalog = JsonDocument.Parse(await signedIn.Content.ReadAsStringAsync());
        using var session = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        var entries = catalog.RootElement.EnumerateArray().ToDictionary(e => e.GetProperty("name").GetString()!, e => e.GetProperty("module").GetString(), StringComparer.Ordinal);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal("settings", entries["settings.write"]);
        Assert.Equal("console", entries["workspace.configure.enter"]);
        Assert.Equal(["settings.read@organization"], session.RootElement.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
    }



    private static HttpRequestMessage WithSite(HttpRequestMessage request, long siteId)
    {
        request.Headers.Add(SiteContext.Header, siteId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return request;
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }
}
