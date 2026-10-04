// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Web.Components;

namespace Wolfgang.Wms.IntegrationTests.Web;

/// <summary>
/// The console host's request localization (E82.4, the E1.14 policy): the culture comes from the picker cookie,
/// then the browser's <c>Accept-Language</c>, never the query string; the resolved culture is echoed in
/// <c>Content-Language</c> and in the page's <c>&lt;html lang&gt;</c>, and an unsupported language falls back to
/// English.
/// </summary>
public sealed class ConsoleLocalizationTests : IClassFixture<WebApplicationFactory<App>>
{
    private readonly WebApplicationFactory<App> _factory;



    public ConsoleLocalizationTests(WebApplicationFactory<App> factory)
    {
        _factory = factory;
    }



    [Theory]
    [InlineData("de-DE")]
    [InlineData("en-GB")]
    public async Task A_page_carries_the_resolved_culture_in_Content_Language_and_html_lang(string acceptLanguage)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(acceptLanguage);

        using var response = await client.GetAsync(new Uri("/configure", UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["en"], response.Content.Headers.ContentLanguage);
        Assert.Contains("<html lang=\"en\">", html, StringComparison.Ordinal);
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



    [Fact]
    public async Task The_error_banner_dismiss_control_is_a_native_button()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync(new Uri("/not-found", UriKind.Relative));

        Assert.Matches("<button type=\"button\" class=\"dismiss\" aria-label=\"[^\"]+\"[^>]*>", html);
    }
}
