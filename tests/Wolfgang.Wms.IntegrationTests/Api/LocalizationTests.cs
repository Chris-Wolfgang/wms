// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E1.14: the API host resolves a request culture from the supported list and echoes it in
/// <c>Content-Language</c>; an unsupported language falls back to English.
/// </summary>
public sealed class LocalizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;



    public LocalizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Theory]
    [InlineData("de-DE", "en")]
    [InlineData("en-GB", "en")]
    public async Task Requests_carry_the_resolved_culture_in_Content_Language(string acceptLanguage, string expected)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(acceptLanguage);

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([expected], response.Content.Headers.ContentLanguage);
    }



    [Fact]
    public void The_culture_comes_from_the_picker_cookie_then_the_browser_never_the_query_string()
    {
        var options = _factory.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;

        Assert.Equal
        (
            [typeof(CookieRequestCultureProvider), typeof(AcceptLanguageHeaderRequestCultureProvider)],
            options.RequestCultureProviders.Select(p => p.GetType())
        );
    }
}
