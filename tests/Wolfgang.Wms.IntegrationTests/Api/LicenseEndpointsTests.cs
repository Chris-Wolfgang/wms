// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Licensing;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E79 through the API without a database: the page shows the compiled-in free tier; a key that is not
/// the vendor's is refused with <c>license.key_rejected</c>; removing an unknown key is 404; the
/// comparison marks the installed tier; an endpoint behind a paid feature answers
/// <c>license.feature_not_licensed</c> on the free tier while a free feature passes.
/// </summary>
public sealed class LicenseEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private readonly WebApplicationFactory<Program> _factory;



    public LicenseEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task The_free_tier_page_refusals_and_the_comparison()
    {
        using var client = _factory.WithTestAuth().CreateClient();
        using var pair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var foreign = LicenseKeySigner.Sign(new LicenseKey(1, "b1", LicenseKeyKind.Base, "pro", "Acme", [new CoveragePeriod(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1))], [], new Dictionary<string, LimitValue>(StringComparer.Ordinal), 0, [], new DateOnly(2026, 1, 1)), pair);

        using var anonymous = await client.GetAsync(new Uri("/api/v0/system/license", UriKind.Relative));
        using var page = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/system/license", "license.read@organization"));
        using var rejected = await client.SendAsync(TestAuth.As(HttpMethod.Put, "/api/v0/system/license/keys", "license.manage@organization", Body(new InstallLicenseKeyRequest(foreign))));
        using var garbage = await client.SendAsync(TestAuth.As(HttpMethod.Put, "/api/v0/system/license/keys", "license.manage@organization", Body(new InstallLicenseKeyRequest("nope"))));
        using var missing = await client.SendAsync(TestAuth.As(HttpMethod.Delete, "/api/v0/system/license/keys/b1", "license.manage@organization"));
        using var readOnly = await client.SendAsync(TestAuth.As(HttpMethod.Delete, "/api/v0/system/license/keys/b1", "license.read@organization"));
        using var comparison = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/system/license/comparison", "license.read@organization"));
        var status = await page.Content.ReadFromJsonAsync<LicenseStatus>(Json);
        var table = await comparison.Content.ReadFromJsonAsync<FeatureComparison>(Json);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(("free", CoverageStatus.Perpetual, 5, 0, ReleaseInfo.Version, 80), (status!.Tier, status.Coverage, status.IncludedDevices, status.PurchasedDevices, status.ReleaseVersion, status.UsageWarningPercent));
        Assert.Contains("backups.core", status.Features);
        Assert.DoesNotContain("workspace.insights", status.Features);
        Assert.Equal(LicenseLimits.All.Select(l => l.Name), status.Limits.Select(l => l.Name));
        Assert.All(status.Limits, l => Assert.Equal((0, LimitOutcome.Allowed, false), (l.Count, l.Outcome, l.Warning)));
        Assert.Empty(status.Keys);
        Assert.Empty(status.Banners);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("license.key_rejected", await CodeAsync(rejected));
        Assert.Equal(HttpStatusCode.BadRequest, garbage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("license.key_not_found", await CodeAsync(missing));
        Assert.Equal(HttpStatusCode.Forbidden, readOnly.StatusCode);
        Assert.Equal(HttpStatusCode.OK, comparison.StatusCode);
        Assert.Equal(("free", 3), (table!.InstalledTier, table.Tiers.Count));
        Assert.Equal(LicenseFeatures.All.Count, table.Features.Count);
    }



    [Fact]
    public async Task A_paid_feature_edge_refuses_the_free_tier()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddWmsApiVersioning();
        builder.Services.AddWmsProblemDetails();
        builder.Services.AddWmsModules();
        builder.Services.AddWmsSettingsModule();
        builder.Services.AddWmsLicenseModule();
        await using var app = builder.Build();
        app.UseWmsProblemDetails();
        var api = app.MapWmsApi();
        api.MapGet("/insights", () => "insights").AllowAnonymous().RequireLicenseFeature(LicenseFeatures.WorkspaceInsights);
        api.MapGet("/reports", () => "reports").AllowAnonymous().RequireLicenseFeature(LicenseFeatures.ReportsBuiltIn);
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var paid = await client.GetAsync(new Uri("/api/v0/insights", UriKind.Relative));
        using var free = await client.GetAsync(new Uri("/api/v0/reports", UriKind.Relative));
        using var problem = JsonDocument.Parse(await paid.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Forbidden, paid.StatusCode);
        Assert.Equal("license.feature_not_licensed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("'workspace.insights' is not included in the free tier.", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.OK, free.StatusCode);
        Assert.Equal("reports", await free.Content.ReadAsStringAsync());
        Assert.Throws<ArgumentNullException>(() => LicenseFeatureEndpointExtensions.RequireLicenseFeature<RouteHandlerBuilder>(null!, LicenseFeatures.Backups));
        Assert.Throws<ArgumentNullException>(() => api.MapGet("/x", () => "x").RequireLicenseFeature(null!));
        await app.StopAsync();
    }



    private static StringContent Body<T>(T value)
    {
        return new StringContent(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }
}
