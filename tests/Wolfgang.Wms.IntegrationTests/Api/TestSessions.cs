// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text;
using System.Text.Json;
using Wolfgang.Wms.Core.Identity;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// Signs the bootstrap administrator in on a database-backed test host: sign in with the documented default,
/// replace it (the must-change gate), and hand back the session cookie to send on every request.
/// </summary>
public static class TestSessions
{
    /// <summary>
    /// The password the administrator ends up with.
    /// </summary>
    public const string AdministratorPassword = "a-long-enough-password";



    /// <summary>
    /// The <c>Cookie</c> header value of an administrator session that has changed its password.
    /// </summary>
    public static async Task<string> SignInAsAdministratorAsync(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        using var login = await client.SendAsync(Json(HttpMethod.Post, "/api/v0/auth/local/login", new LocalLoginRequest("admin", PasswordPolicy.BootstrapDefault), cookie: null));
        if (login.StatusCode == HttpStatusCode.Unauthorized)
        {
            using var again = await client.SendAsync(Json(HttpMethod.Post, "/api/v0/auth/local/login", new LocalLoginRequest("admin", AdministratorPassword), cookie: null));   // already changed by an earlier host on this database
            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
            return Cookie(again);
        }

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var change = await client.SendAsync(Json(HttpMethod.Post, "/api/v0/auth/local/password", new ChangePasswordRequest(PasswordPolicy.BootstrapDefault, AdministratorPassword), Cookie(login)));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        return Cookie(change);
    }



    private static HttpRequestMessage Json<T>(HttpMethod method, string path, T body, string? cookie)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative))
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
        };
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return request;
    }



    private static string Cookie(HttpResponseMessage response)
    {
        return response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("wms.session=", StringComparison.Ordinal)).Split(';')[0];
    }
}
