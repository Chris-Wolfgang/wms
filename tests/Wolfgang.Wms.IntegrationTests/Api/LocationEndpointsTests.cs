// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E17.1 on the real API host before a database is configured: the endpoints answer <c>auth.not_signed_in</c>
/// without a session, <c>locations.unavailable</c> with a grant, 403 with the wrong grant; the <c>siteId</c>
/// route value scopes a site-level grant; and a malformed page request (unknown sort, both cursors, a cursor
/// this API did not issue) is refused with <c>locations.invalid</c> before the store is asked.
/// </summary>
public sealed class LocationEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Draft = """{ "code": "A-01-01", "barcode": "LOC1", "zoneId": 1, "walkSequence": "A-0101" }""";
    private readonly WebApplicationFactory<Program> _factory;



    public LocationEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task Without_a_database_the_locations_are_unavailable_and_a_bad_page_request_is_refused_first()
    {
        using var anonymous = _factory.CreateClient();
        using var granted = _factory.WithTestAuth().CreateClient();

        using var notSignedIn = await anonymous.GetAsync(new Uri("/api/v0/sites/1/locations", UriKind.Relative));
        using var list = await granted.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/sites/1/locations", "locations.read@organization"));
        using var create = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/locations", "locations.write@organization", Json(Draft)));
        using var forbidden = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/locations", "locations.read@organization", Json(Draft)));
        using var ownSite = await granted.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/sites/3/locations/9", "locations.read@site:3"));
        using var otherSite = await granted.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/sites/4/locations/9", "locations.read@site:3"));
        using var badSort = await granted.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/sites/1/locations?sort=colour", "locations.read@organization"));
        using var bothCursors = await granted.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/sites/1/locations?after=a&before=b", "locations.read@organization"));
        using var badCursor = await granted.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/sites/1/locations?after=not-a-cursor", "locations.read@organization"));

        Assert.Equal(HttpStatusCode.Unauthorized, notSignedIn.StatusCode);
        Assert.Equal("auth.not_signed_in", await CodeAsync(notSignedIn));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, list.StatusCode);
        Assert.Equal("locations.unavailable", await CodeAsync(list));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ownSite.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherSite.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badSort.StatusCode);
        Assert.Equal("locations.invalid", await CodeAsync(badSort));
        Assert.Contains("walk_sequence", await DetailAsync(badSort), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, bothCursors.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badCursor.StatusCode);
        Assert.Contains("not a cursor this API issued", await DetailAsync(badCursor), StringComparison.Ordinal);
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



    private static async Task<string> DetailAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("detail").GetString() ?? string.Empty;
    }
}
