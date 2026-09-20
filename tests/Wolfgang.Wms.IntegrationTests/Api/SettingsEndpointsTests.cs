// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E6.3 on the real API host before a database is configured: values read as defaults, writes answer the
/// <c>settings.store_unavailable</c> problem, unknown scopes and keys answer theirs.
/// </summary>
public sealed class SettingsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly SettingKey<int> MaxTotes = new("sample.max_totes", 3, "Totes a picker may carry.");
    private readonly WebApplicationFactory<Program> _host;



    public SettingsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddWmsModule(ModuleDescriptor.Create("sample").WithSettings(MaxTotes))));
    }



    [Fact]
    public async Task Values_read_as_defaults_without_a_database()
    {
        using var client = _host.CreateClient();

        using var list = await client.GetAsync(new Uri("/api/v0/settings/site/4", UriKind.Relative));
        using var one = await client.GetAsync(new Uri("/api/v0/settings/organization/0/sample.max_totes", UriKind.Relative));
        using var items = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        using var value = JsonDocument.Parse(await one.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal("sample.max_totes", items.RootElement[0].GetProperty("name").GetString());
        Assert.Equal("site:4", items.RootElement[0].GetProperty("scope").GetString());
        Assert.Equal("3", items.RootElement[0].GetProperty("effectiveValue").GetString());
        Assert.Equal("default", items.RootElement[0].GetProperty("inheritedFrom").GetString());
        Assert.Equal(JsonValueKind.Null, items.RootElement[0].GetProperty("etag").ValueKind);
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        Assert.Equal("Integer", value.RootElement.GetProperty("kind").GetString());
        Assert.Null(one.Headers.ETag);
    }



    [Fact]
    public async Task Writes_answer_store_unavailable_and_bad_routes_answer_their_codes()
    {
        using var client = _host.CreateClient();

        using var put = await client.PutAsync(new Uri("/api/v0/settings/organization/0/sample.max_totes", UriKind.Relative), Json("{\"value\":\"5\"}"));
        using var delete = await client.DeleteAsync(new Uri("/api/v0/settings/organization/0/sample.max_totes", UriKind.Relative));
        using var badScope = await client.GetAsync(new Uri("/api/v0/settings/aisle/1", UriKind.Relative));
        using var badKey = await client.GetAsync(new Uri("/api/v0/settings/organization/0/sample.nope", UriKind.Relative));
        using var badValue = await client.PutAsync(new Uri("/api/v0/settings/organization/0/sample.max_totes", UriKind.Relative), Json("{\"value\":\"many\"}"));
        using var badMode = await client.PutAsync(new Uri("/api/v0/settings/organization/0/sample.max_totes", UriKind.Relative), Json("{\"mode\":\"per_aisle\"}"));
        using var modeUnknownKey = await client.PutAsync(new Uri("/api/v0/settings/organization/0/sample.nope", UriKind.Relative), Json("{\"mode\":\"per_site\"}"));
        using var mode = await client.PutAsync(new Uri("/api/v0/settings/organization/0/sample.max_totes", UriKind.Relative), Json("{\"mode\":\"per_site\"}"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, put.StatusCode);
        Assert.Equal("settings.store_unavailable", await CodeAsync(put));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, delete.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badScope.StatusCode);
        Assert.Equal("settings.unknown_scope", await CodeAsync(badScope));
        Assert.Equal(HttpStatusCode.NotFound, badKey.StatusCode);
        Assert.Equal("settings.unknown_key", await CodeAsync(badKey));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, badValue.StatusCode);   // the store is checked before the value without a database
        Assert.Equal(HttpStatusCode.BadRequest, badMode.StatusCode);
        Assert.Equal("settings.mode_not_allowed", await CodeAsync(badMode));
        Assert.Equal(HttpStatusCode.NotFound, modeUnknownKey.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, mode.StatusCode);
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
