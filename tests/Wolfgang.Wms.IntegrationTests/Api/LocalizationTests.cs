// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

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
}
