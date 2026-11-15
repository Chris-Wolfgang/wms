// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Wolfgang.Wms.Core.Identity.BreakGlass;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E9 on the real API host before a database is configured: sign-in answers <c>auth.unavailable</c>, the
/// signed-in-only endpoints answer <c>auth.not_signed_in</c> as problems (never redirects), sign-out works,
/// and the break-glass gate reports local sign-in open (E9.3: nothing is stored without a database).
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



    [Fact]
    public async Task Without_a_database_the_local_sign_in_gate_reports_open_and_unverified()
    {
        using var client = _factory.CreateClient();

        var status = await client.GetFromJsonAsync<LocalLoginStatus>("/api/v0/auth/local/status", JsonSerializerOptions.Web);

        Assert.Equal(new LocalLoginStatus(LocalLoginOpen: true, SsoVerified: false, UnlockedUntil: null, ForcedLocal: false), status);
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
