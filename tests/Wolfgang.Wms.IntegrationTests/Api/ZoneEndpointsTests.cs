// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E16.2 on the real API host before a database is configured: the endpoints answer <c>auth.not_signed_in</c>
/// without a session, <c>zones.unavailable</c> with a grant, 403 with the wrong grant; and the <c>siteId</c>
/// route value scopes a site-level grant to that one site's zones.
/// </summary>
public sealed class ZoneEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Draft = """{ "code": "A01", "name": "Aisle 1", "type": "Pick", "walkOrderPrefix": "A", "isRejectLane": false, "resolution": null }""";
    private readonly WebApplicationFactory<Program> _factory;



    public ZoneEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task Without_a_database_the_zones_are_unavailable_and_grants_are_scoped_by_the_route()
    {
        using var anonymous = _factory.CreateClient();
        using var granted = _factory.WithTestAuth().CreateClient();

        using var notSignedIn = await anonymous.GetAsync(new Uri("/api/v0/sites/1/zones", UriKind.Relative));
        using var list = await granted.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/sites/1/zones", "zones.read@organization"));
        using var create = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/zones", "zones.write@organization", Json(Draft)));
        using var forbidden = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/zones", "zones.read@organization", Json(Draft)));
        using var ownSite = await granted.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/sites/3/zones/9", "zones.read@site:3"));
        using var otherSite = await granted.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/sites/4/zones/9", "zones.read@site:3"));

        Assert.Equal(HttpStatusCode.Unauthorized, notSignedIn.StatusCode);
        Assert.Equal("auth.not_signed_in", await CodeAsync(notSignedIn));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, list.StatusCode);
        Assert.Equal("zones.unavailable", await CodeAsync(list));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ownSite.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherSite.StatusCode);
    }



    private static StringContent Json(string body)
    {
        return new StringContent(body, Encoding.UTF8, "application/json");
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }
}
