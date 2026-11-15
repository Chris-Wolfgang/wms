// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E16.0 on the real API host before a database is configured: the anonymous view answers
/// <c>organization.unavailable</c>, the signed-in endpoints answer <c>auth.not_signed_in</c> as problems,
/// and the test scheme's grants reach the store, which answers the same problem.
/// </summary>
public sealed class OrganizationEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;



    public OrganizationEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task Without_a_database_the_organization_is_unavailable()
    {
        using var anonymous = _factory.CreateClient();
        using var granted = _factory.WithTestAuth().CreateClient();

        using var publicView = await anonymous.GetAsync(new Uri("/api/v0/organization/public", UriKind.Relative));
        using var notSignedIn = await anonymous.GetAsync(new Uri("/api/v0/organization", UriKind.Relative));
        using var read = await granted.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/organization", "organization.read@organization"));
        using var create = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/organization", "organization.write@organization", Json("""{ "name": "Acme", "timeZone": "UTC", "locale": "en-US" }""")));
        using var forbidden = await granted.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/organization", "organization.read@organization", Json("""{ "name": "Acme", "timeZone": "UTC", "locale": "en-US" }""")));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, publicView.StatusCode);
        Assert.Equal("organization.unavailable", await CodeAsync(publicView));
        Assert.Equal(HttpStatusCode.Unauthorized, notSignedIn.StatusCode);
        Assert.Equal("auth.not_signed_in", await CodeAsync(notSignedIn));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, read.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
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
