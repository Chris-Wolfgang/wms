// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E6.1 on the real API host: the registry endpoint lists the settings every registered module declares,
/// with kind, scopes, default, description and choices, and is read-only.
/// </summary>
public sealed class SettingsRegistryEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;



    public SettingsRegistryEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task Registry_lists_the_settings_modules_declare()
    {
        using var host = _factory.WithTestAuth().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddWmsModule
            (
                ModuleDescriptor
                    .Create("sample")
                    .WithSettings
                    (
                        new SettingKey<TimeSpan>("sample.lease_timeout", TimeSpan.FromMinutes(15), "How long a picker holds a task.") { TriggersDeviceResync = true },
                        new SettingKey<SettingScope>("sample.level", SettingScope.Site, "A choice.") { Scopes = SettingScopes.OrganizationToSku, RequiresRestart = true }
                    )
            )));
        using var client = host.CreateClient();

        using var response = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/settings/registry", "settings.read@organization"));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var entries = document.RootElement.EnumerateArray().ToDictionary(e => e.GetProperty("name").GetString()!, e => e, StringComparer.Ordinal);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["sample.lease_timeout", "sample.level"], entries.Keys.Where(k => k.StartsWith("sample.", StringComparison.Ordinal)).Order(StringComparer.Ordinal));   // the host's own modules (auth) list theirs too
        Assert.Equal("Duration", entries["sample.lease_timeout"].GetProperty("kind").GetString());
        Assert.Equal("00:15:00", entries["sample.lease_timeout"].GetProperty("default").GetString());
        Assert.True(entries["sample.lease_timeout"].GetProperty("triggersDeviceResync").GetBoolean());
        Assert.Equal(["organization", "site", "zone"], entries["sample.lease_timeout"].GetProperty("scopes").EnumerateArray().Select(s => s.GetString()));
        Assert.Equal(["organization", "site", "sku"], entries["sample.level"].GetProperty("scopes").EnumerateArray().Select(s => s.GetString()));
        Assert.Equal(["Organization", "Site", "Zone", "Sku"], entries["sample.level"].GetProperty("choices").EnumerateArray().Select(s => s.GetString()));
        Assert.True(entries["sample.level"].GetProperty("requiresRestart").GetBoolean());
    }



    [Fact]
    public async Task Registry_holds_only_the_hosts_own_settings_and_is_read_only_without_other_modules()
    {
        using var host = _factory.WithTestAuth();
        using var client = host.CreateClient();

        using var response = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/settings/registry", "*@organization"));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        using var post = await client.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/settings/registry", "*@organization", new StringContent("[]")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(document.RootElement.EnumerateArray(), e => Assert.StartsWith("auth.", e.GetProperty("name").GetString(), StringComparison.Ordinal));   // E9: the auth module's settings
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }
}
