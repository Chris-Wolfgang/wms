// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Database.Settings;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E6.3 against real engines through the API and the typed accessor: defaults, first write without a row,
/// <c>If-Match</c> on later writes, inheritance down a test hierarchy, a configured child shielding its
/// subtree from a parent change, reset restoring inheritance, validation and scope errors, secret masking,
/// and the cache serving reads between writes.
/// </summary>
public sealed class EfSettingsTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private static readonly SettingKey<TimeSpan> LeaseTimeout = new("sample.lease_timeout", TimeSpan.FromMinutes(15), "How long a picker holds a task.")
    {
        Validator = v => v >= TimeSpan.FromMinutes(1) ? null : "must be at least 1 minute",
    };
    private static readonly SettingKey<SecretText> Password = new("sample.password", new SecretText(string.Empty), "A credential.") { Scopes = SettingScopes.Organization };
    private static readonly SettingKey<SettingScope> Level = new("sample.level", SettingScope.Site, "A choice.") { Scopes = SettingScopes.OrganizationToSku };
    private static readonly SettingScopeRef Site1 = SettingScopeRef.Site(1);
    private static readonly SettingScopeRef Site2 = SettingScopeRef.Site(2);
    private static readonly SettingScopeRef Zone10 = SettingScopeRef.Zone(10);
    private static readonly SettingScopeRef Sku5 = SettingScopeRef.Sku(5);



    [DockerFact]
    public async Task SqlServer_reads_writes_cascades_and_caches()
    {
        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync();

        await AssertSettingsAsync("SqlServer", container.GetConnectionString(), trustServerCertificate: true);
    }



    [DockerFact]
    public async Task PostgreSql_reads_writes_cascades_and_caches()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();

        await AssertSettingsAsync("PostgreSql", container.GetConnectionString(), trustServerCertificate: false);
    }



    private static async Task AssertSettingsAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        await using var app = await StartHostAsync(provider, connectionString, trustServerCertificate);
        using var client = app.GetTestClient();

        var etag = await AssertFirstWriteAndPreconditionsAsync(client);
        await AssertInheritanceAndShieldingAsync(client, etag);
        await AssertValidationAndSecretsAsync(client);
        await AssertTypedAccessAndCacheAsync(app.Services);
    }



    private static async Task<string> AssertFirstWriteAndPreconditionsAsync(HttpClient client)
    {
        var defaults = (await client.GetFromJsonAsync<List<SettingValue>>("/api/v0/settings/organization/0", Json))!;
        Assert.Equal(["sample.lease_timeout", "sample.level", "sample.password"], defaults.Select(v => v.Name));
        Assert.All(defaults, v => Assert.Equal(SettingValue.DefaultSource, v.InheritedFrom));

        using var first = await PutAsync(client, "organization/0", "sample.lease_timeout", "00:30:00", ifMatch: null);
        var written = (await first.Content.ReadFromJsonAsync<SettingValue>(Json))!;
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("00:30:00", written.ConfiguredValue);
        Assert.Equal("00:30:00", written.EffectiveValue);
        Assert.Null(written.InheritedFrom);
        Assert.Equal(SettingsModule.AnonymousUser, written.UpdatedBy);
        Assert.NotNull(written.Etag);
        Assert.Equal(written.Etag, first.Headers.ETag?.ToString());

        using var missing = await PutAsync(client, "organization/0", "sample.lease_timeout", "00:31:00", ifMatch: null);
        using var stale = await PutAsync(client, "organization/0", "sample.lease_timeout", "00:31:00", ifMatch: EntityTag.FromRowVersion(ulong.MaxValue).Value);   // the first row is version 1, so "1" would match
        using var current = await PutAsync(client, "organization/0", "sample.lease_timeout", "00:31:00", ifMatch: written.Etag);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var updated = (await current.Content.ReadFromJsonAsync<SettingValue>(Json))!;
        Assert.NotEqual(written.Etag, updated.Etag);
        return updated.Etag!;
    }



    private static async Task AssertInheritanceAndShieldingAsync(HttpClient client, string organisationEtag)
    {
        var site1 = await GetAsync(client, "site/1", "sample.lease_timeout");
        Assert.Equal("00:31:00", site1.EffectiveValue);
        Assert.Equal("organization", site1.InheritedFrom);
        Assert.Null(site1.ConfiguredValue);

        using var overrideSite1 = await PutAsync(client, "site/1", "sample.lease_timeout", "00:10:00", ifMatch: null);
        Assert.Equal(HttpStatusCode.OK, overrideSite1.StatusCode);
        var zone10 = await GetAsync(client, "zone/10", "sample.lease_timeout");
        Assert.Equal("00:10:00", zone10.EffectiveValue);
        Assert.Equal("site:1", zone10.InheritedFrom);

        // site 2 gets a row that inherits (write, then reset) so the cascade has a materialised row to rewrite.
        using var site2Write = await PutAsync(client, "site/2", "sample.lease_timeout", "00:05:00", ifMatch: null);
        var site2Row = (await site2Write.Content.ReadFromJsonAsync<SettingValue>(Json))!;
        using var site2Reset = await client.SendAsync(Request(HttpMethod.Delete, "site/2", "sample.lease_timeout", null, site2Row.Etag));
        Assert.Equal(HttpStatusCode.OK, site2Reset.StatusCode);
        var site2AfterReset = (await site2Reset.Content.ReadFromJsonAsync<SettingValue>(Json))!;
        Assert.Null(site2AfterReset.ConfiguredValue);
        Assert.Equal("00:31:00", site2AfterReset.EffectiveValue);

        using var organisationChange = await PutAsync(client, "organization/0", "sample.lease_timeout", "00:20:00", ifMatch: organisationEtag);
        Assert.Equal(HttpStatusCode.OK, organisationChange.StatusCode);
        Assert.Equal("00:10:00", (await GetAsync(client, "site/1", "sample.lease_timeout")).EffectiveValue);   // shielded by its own value
        Assert.Equal("00:10:00", (await GetAsync(client, "zone/10", "sample.lease_timeout")).EffectiveValue);
        var site2Cascaded = await GetAsync(client, "site/2", "sample.lease_timeout");
        Assert.Equal("00:20:00", site2Cascaded.EffectiveValue);
        Assert.NotEqual(site2AfterReset.Etag, site2Cascaded.Etag);   // the materialised row was rewritten in the same transaction

        var site1Row = await GetAsync(client, "site/1", "sample.lease_timeout");
        using var site1Reset = await client.SendAsync(Request(HttpMethod.Delete, "site/1", "sample.lease_timeout", null, site1Row.Etag));
        Assert.Equal(HttpStatusCode.OK, site1Reset.StatusCode);
        Assert.Equal("00:20:00", (await GetAsync(client, "zone/10", "sample.lease_timeout")).EffectiveValue);
        using var resetAgain = await client.SendAsync(Request(HttpMethod.Delete, "site/1", "sample.lease_timeout", null, (await GetAsync(client, "site/1", "sample.lease_timeout")).Etag));
        Assert.Equal(HttpStatusCode.OK, resetAgain.StatusCode);   // resetting an inheriting row is a no-op
    }



    private static async Task AssertValidationAndSecretsAsync(HttpClient client)
    {
        using var notADuration = await PutAsync(client, "organization/0", "sample.level", "aisle", ifMatch: null);
        using var tooShort = await PutAsync(client, "site/3", "sample.lease_timeout", "00:00:10", ifMatch: null);
        using var wrongScope = await PutAsync(client, "site/3", "sample.password", "hunter2", ifMatch: null);
        using var secret = await PutAsync(client, "organization/0", "sample.password", "hunter2", ifMatch: null);
        using var unknown = await PutAsync(client, "organization/0", "sample.nope", "x", ifMatch: null);

        Assert.Equal(HttpStatusCode.BadRequest, notADuration.StatusCode);
        Assert.Equal("settings.invalid_value", await CodeAsync(notADuration));
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Contains("at least 1 minute", await DetailAsync(tooShort), StringComparison.Ordinal);
        Assert.Equal("settings.scope_not_allowed", await CodeAsync(wrongScope));
        Assert.Equal(HttpStatusCode.OK, secret.StatusCode);
        Assert.Equal("••••••", (await secret.Content.ReadFromJsonAsync<SettingValue>(Json))!.EffectiveValue);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }



    private static async Task AssertTypedAccessAndCacheAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettings>();
        var cache = services.GetRequiredService<SettingsCache>();

        Assert.Equal(TimeSpan.FromMinutes(20), await settings.GetAsync(LeaseTimeout, Zone10, CancellationToken.None));
        Assert.Equal("hunter2", (await settings.GetAsync(Password, SettingScopeRef.Organization, CancellationToken.None)).Value);
        Assert.NotNull(cache.CachedVersion);

        var before = cache.CachedVersion;
        var set = await settings.SetAsync(Level, Sku5, SettingScope.Zone, "tester", CancellationToken.None);
        Assert.Equal("Zone", set.ConfiguredValue);
        Assert.Equal("tester", set.UpdatedBy);
        Assert.NotEqual(before, cache.CachedVersion);
        Assert.Equal(SettingScope.Zone, await settings.GetAsync(Level, Sku5, CancellationToken.None));
        Assert.Equal(SettingScope.Site, await settings.GetAsync(Level, Site2, CancellationToken.None));

        var reset = await settings.ResetAsync(Level, Sku5, "tester", CancellationToken.None);
        Assert.Null(reset.ConfiguredValue);
        Assert.Equal("Site", reset.EffectiveValue);
        var failure = await Assert.ThrowsAsync<SettingException>(() => settings.SetAsync(Password, Site1, new SecretText("x"), "tester", CancellationToken.None));
        Assert.Equal(SettingErrorCodes.ScopeNotAllowed, failure.Code);
        var invalid = await Assert.ThrowsAsync<SettingException>(() => settings.SetAsync(LeaseTimeout, Site1, TimeSpan.Zero, "tester", CancellationToken.None));
        Assert.Equal(SettingErrorCodes.InvalidValue, invalid.Code);
        await Assert.ThrowsAsync<ArgumentException>(() => settings.SetAsync(LeaseTimeout, Site1, TimeSpan.FromMinutes(2), " ", CancellationToken.None));

        var context = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        Assert.Throws<ArgumentException>(() => MaxRowVersionSource.Sql(context, "core.nothing"));
        Assert.Contains("row_version", MaxRowVersionSource.Sql(context, SettingsCache.Table), StringComparison.Ordinal);
    }



    private static async Task<WebApplication> StartHostAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Wms:Database:Provider"] = provider,
            ["Wms:Database:ConnectionString"] = connectionString,
            ["Wms:Database:TrustServerCertificate"] = trustServerCertificate ? "true" : "false",
            ["Wms:Database:AutoMigrate"] = "true",
        });
        builder.Services.AddWmsApiVersioning();
        builder.Services.AddWmsProblemDetails();
        builder.Services.AddWmsModules();
        builder.Services.AddWmsSettingsModule();
        builder.Services.AddWmsModule(ModuleDescriptor.Create("sample").WithSettings(LeaseTimeout, Password, Level));
        builder.Services.AddSingleton<ISettingScopeHierarchy, TestHierarchy>();
        builder.Services.AddWmsDatabase(builder.Configuration);
        var app = builder.Build();
        app.UseWmsProblemDetails();
        app.MapWmsApi().MapWmsModules();
        await app.StartAsync();
        return app;
    }



    private static async Task<SettingValue> GetAsync(HttpClient client, string scope, string key)
    {
        return (await client.GetFromJsonAsync<SettingValue>($"/api/v0/settings/{scope}/{key}", Json))!;
    }



    private static Task<HttpResponseMessage> PutAsync(HttpClient client, string scope, string key, string value, string? ifMatch)
    {
        return client.SendAsync(Request(HttpMethod.Put, scope, key, value, ifMatch));
    }



    private static HttpRequestMessage Request(HttpMethod method, string scope, string key, string? value, string? ifMatch)
    {
        var request = new HttpRequestMessage(method, new Uri($"/api/v0/settings/{scope}/{key}", UriKind.Relative));
        if (value is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(new SetSettingRequest(value), Json), Encoding.UTF8, "application/json");
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return request;
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }



    private static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("detail").GetString();
    }



    /// <summary>
    /// organisation → sites 1, 2; site 1 → zone 10; site 2 → SKU 5.
    /// </summary>
    private sealed class TestHierarchy : ISettingScopeHierarchy
    {
        public Task<SettingScopeRef?> ParentAsync(SettingScopeRef scope, CancellationToken cancellationToken)
        {
            SettingScopeRef? parent = scope switch
            {
                { Type: SettingScope.Site } => SettingScopeRef.Organization,
                { Type: SettingScope.Zone, Id: 10 } => Site1,
                { Type: SettingScope.Sku, Id: 5 } => Site2,
                _ => null,
            };
            return Task.FromResult(parent);
        }



        public Task<IReadOnlyList<SettingScopeRef>> ChildrenAsync(SettingScopeRef scope, CancellationToken cancellationToken)
        {
            IReadOnlyList<SettingScopeRef> children = scope switch
            {
                { Type: SettingScope.Organization } => [Site1, Site2],
                { Type: SettingScope.Site, Id: 1 } => [Zone10],
                { Type: SettingScope.Site, Id: 2 } => [Sku5],
                _ => [],
            };
            return Task.FromResult(children);
        }
    }
}
