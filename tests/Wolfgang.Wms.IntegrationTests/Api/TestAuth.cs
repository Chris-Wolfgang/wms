// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// A test authentication scheme for hosts without a database: a request carrying <c>X-Test-Permissions</c>
/// (comma-separated grants such as <c>*@organization</c> or <c>settings.read@site:3</c>) is signed in as
/// "tester" with those grants; a request without it is anonymous. Challenges and forbids answer the same
/// problems the product does.
/// </summary>
public static class TestAuth
{
    public const string Scheme = "Test";

    public const string PermissionsHeader = "X-Test-Permissions";



    /// <summary>
    /// A factory whose host authenticates with the test scheme.
    /// </summary>
    public static WebApplicationFactory<Program> WithTestAuth(this WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(Scheme, displayName: null, configureOptions: null);
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = Scheme;
                options.DefaultChallengeScheme = Scheme;
                options.DefaultForbidScheme = Scheme;
            });
        }));
    }



    /// <summary>
    /// A request carrying the given grants.
    /// </summary>
    public static HttpRequestMessage As(HttpMethod method, string path, string? grants, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative)) { Content = content };
        if (grants is not null)
        {
            request.Headers.Add(PermissionsHeader, grants);
        }

        return request;
    }



    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(PermissionsHeader, out var header))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var grants = header.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var identity = new ClaimsIdentity(TestAuth.Scheme, SessionClaims.UserName, roleType: null);
            identity.AddClaim(new Claim(SessionClaims.UserId, "1"));
            identity.AddClaim(new Claim(SessionClaims.UserName, "tester"));
            identity.AddClaim(new Claim(SessionClaims.DisplayName, "Tester"));
            identity.AddClaim(new Claim(SessionClaims.MustChangePassword, "false"));
            identity.AddClaim(new Claim(SessionClaims.LocalAdmin, "false"));
            foreach (var grant in grants)
            {
                identity.AddClaim(new Claim(PermissionClaims.ClaimType, grant));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), TestAuth.Scheme)));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            return ApiProblems.Problem(AuthErrorCodes.NotSignedIn).ExecuteAsync(Context);
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            return ApiProblems.Problem(AuthErrorCodes.Forbidden).ExecuteAsync(Context);
        }
    }
}
