// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E16.6 on the real API host before a database is configured: the endpoints answer <c>auth.not_signed_in</c>
/// without a session, <c>imports.unavailable</c> with the grant, 403 without it; and a malformed request (an
/// empty file, an unknown policy, an unknown format) is refused with <c>imports.invalid</c> before the importer
/// is asked.
/// </summary>
public sealed class ImportEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Zones = """[{ "code": "A01", "name": "Aisle 1" }]""";
    private const string Locations = """[{ "code": "A-01-01", "barcode": "L1", "zoneCode": "A01", "walkSequence": "A-0101" }]""";
    private readonly WebApplicationFactory<Program> _factory;



    public ImportEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task Without_a_database_the_imports_are_unavailable_and_a_bad_request_is_refused_first()
    {
        using var anonymous = _factory.CreateClient();
        using var granted = _factory.WithTestAuth().CreateClient();

        using var notSignedIn = await anonymous.PostAsync(new Uri("/api/v0/sites/1/imports/zones", UriKind.Relative), Json(Zones));
        using var zones = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/imports/zones", "imports.write@organization", Json(Zones)));
        using var locations = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/imports/locations?policy=validate_only&format=csv", "imports.write@organization", Json(Locations)));
        using var forbidden = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/imports/zones", "zones.write@organization", Json(Zones)));
        using var empty = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/imports/zones", "imports.write@organization", Json("[]")));
        using var badPolicy = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/imports/zones?policy=maybe", "imports.write@organization", Json(Zones)));
        using var badFormat = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/sites/1/imports/zones?format=xml", "imports.write@organization", Json(Zones)));

        Assert.Equal(HttpStatusCode.Unauthorized, notSignedIn.StatusCode);
        Assert.Equal("auth.not_signed_in", await CodeAsync(notSignedIn));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, zones.StatusCode);
        Assert.Equal("imports.unavailable", await CodeAsync(zones));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, locations.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal("imports.invalid", await CodeAsync(empty));
        Assert.Equal(HttpStatusCode.BadRequest, badPolicy.StatusCode);
        Assert.Contains("all_or_nothing", await DetailAsync(badPolicy), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, badFormat.StatusCode);
        Assert.Contains("format must be json or csv", await DetailAsync(badFormat), StringComparison.Ordinal);
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
