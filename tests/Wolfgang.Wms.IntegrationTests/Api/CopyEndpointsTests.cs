// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E16.5 on the real API host before a database is configured: each copy endpoint answers
/// <c>auth.not_signed_in</c> without a session, <c>copies.unavailable</c> with the entity's write permission,
/// and 403 with only its read permission.
/// </summary>
public sealed class CopyEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;



    public CopyEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task Without_a_database_the_copies_are_unavailable_and_each_needs_its_entitys_write_permission()
    {
        using var anonymous = _factory.CreateClient();
        using var granted = _factory.WithTestAuth().CreateClient();

        using var notSignedIn = await anonymous.PostAsync(new Uri("/api/v0/sites/1/copy", UriKind.Relative), Json("""{ "code": "DC2", "name": "Second" }"""));
        using var site = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/copy", "sites.write@organization", Json("""{ "code": "DC2", "name": "Second" }""")));
        using var siteForbidden = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/copy", "sites.read@organization", Json("""{ "code": "DC2", "name": "Second" }""")));
        using var zone = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/zones/2/copy", "zones.write@site:1", Json("""{ "code": "A02", "name": "Aisle 2" }""")));
        using var zoneForbidden = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/zones/2/copy", "zones.write@site:3", Json("""{ "code": "A02", "name": "Aisle 2" }""")));
        using var location = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/locations/3/copy", "locations.write@organization", Json("""{ "code": "A-02-01", "barcode": "L9" }""")));
        using var range = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/locations/copy-range", "locations.write@organization", Json("""{ "codePrefixFrom": "A-", "codePrefixTo": "B-" }""")));
        using var rangeForbidden = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/locations/copy-range", "locations.read@organization", Json("""{ "codePrefixFrom": "A-", "codePrefixTo": "B-" }""")));

        Assert.Equal(HttpStatusCode.Unauthorized, notSignedIn.StatusCode);
        Assert.Equal("auth.not_signed_in", await CodeAsync(notSignedIn));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, site.StatusCode);
        Assert.Equal("copies.unavailable", await CodeAsync(site));
        Assert.Equal(HttpStatusCode.Forbidden, siteForbidden.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, zone.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, zoneForbidden.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, location.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, range.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rangeForbidden.StatusCode);
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
