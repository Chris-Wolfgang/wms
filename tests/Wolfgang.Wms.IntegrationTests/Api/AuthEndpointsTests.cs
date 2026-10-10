// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E9 on the real API host before a database is configured: sign-in answers <c>auth.unavailable</c>, the
/// signed-in-only endpoints answer <c>auth.not_signed_in</c> as problems (never redirects), sign-out works.
/// </summary>
public sealed class AuthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;



    public AuthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task Without_a_database_sign_in_is_unavailable_and_protected_endpoints_answer_401_problems()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var login = await client.PostAsync(new Uri("/api/v0/auth/local/login", UriKind.Relative), Json("""{ "userName": "admin", "password": "x" }"""));
        using var me = await client.GetAsync(new Uri("/api/v0/auth/me", UriKind.Relative));
        using var password = await client.PostAsync(new Uri("/api/v0/auth/local/password", UriKind.Relative), Json("""{ "currentPassword": "x", "newPassword": "y" }"""));
        using var logout = await client.PostAsync(new Uri("/api/v0/auth/logout", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
        Assert.Equal("auth.unavailable", await CodeAsync(login));
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal("auth.not_signed_in", await CodeAsync(me));
        Assert.Equal(HttpStatusCode.Unauthorized, password.StatusCode);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
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
