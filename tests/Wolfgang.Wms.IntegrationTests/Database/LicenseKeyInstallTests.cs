// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Licensing;
using Wolfgang.Wms.Core.Logging;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Licensing;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.IntegrationTests.Api;
using Wolfgang.Wms.Logging;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E79.3, E79.11 against PostgreSQL: the administrator installs a pro base key and a device add-on signed
/// with the test pair (the host verifies with that pair's public key); the page shows "5 included + 10
/// purchased"; a key with the same id replaces the old one; removing the add-on drops the devices; the
/// stored setting is the secret kind, so it is encrypted at rest and masked on the settings page.
/// </summary>
public sealed class LicenseKeyInstallTests
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;



    [DockerFact]
    public async Task Keys_install_replace_and_remove_without_a_restart()
    {
        using var pair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();
        await using var app = await StartHostAsync(container.GetConnectionString(), Convert.ToBase64String(pair.ExportSubjectPublicKeyInfo()));
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", await TestSessions.SignInAsAdministratorAsync(client));
        var coverage = new[] { new CoveragePeriod(new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31)) };
        var pro = LicenseKeySigner.Sign(new LicenseKey(1, "b1", LicenseKeyKind.Base, "pro", "Acme", coverage, [], new Dictionary<string, LimitValue>(StringComparer.Ordinal), 0, [], new DateOnly(2026, 1, 1)), pair);
        var addOn = LicenseKeySigner.Sign(new LicenseKey(1, "a1", LicenseKeyKind.AddOn, null, "Acme", [], [], new Dictionary<string, LimitValue>(StringComparer.Ordinal), 10, [], new DateOnly(2026, 1, 2)), pair);
        var enterprise = LicenseKeySigner.Sign(new LicenseKey(1, "b1", LicenseKeyKind.Base, "enterprise", "Acme", coverage, [], new Dictionary<string, LimitValue>(StringComparer.Ordinal), 0, [], new DateOnly(2026, 2, 1)), pair);

        using var installed = await client.PutAsync(new Uri("/api/v0/system/license/keys", UriKind.Relative), Body(new InstallLicenseKeyRequest(pro)));
        using var stacked = await client.PutAsync(new Uri("/api/v0/system/license/keys", UriKind.Relative), Body(new InstallLicenseKeyRequest(addOn)));
        using var replaced = await client.PutAsync(new Uri("/api/v0/system/license/keys", UriKind.Relative), Body(new InstallLicenseKeyRequest(enterprise)));
        using var removed = await client.DeleteAsync(new Uri("/api/v0/system/license/keys/a1", UriKind.Relative));
        using var settingPage = await client.GetAsync(new Uri("/api/v0/settings/organization/0/license.keys", UriKind.Relative));
        var afterInstall = await installed.Content.ReadFromJsonAsync<LicenseStatus>(Json);
        var afterStack = await stacked.Content.ReadFromJsonAsync<LicenseStatus>(Json);
        var afterReplace = await replaced.Content.ReadFromJsonAsync<LicenseStatus>(Json);
        var afterRemove = await removed.Content.ReadFromJsonAsync<LicenseStatus>(Json);
        var setting = await settingPage.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, installed.StatusCode);
        Assert.Equal(("pro", "Acme", CoverageStatus.Covered, new DateOnly(2027, 12, 31), 1), (afterInstall!.Tier, afterInstall.Organization, afterInstall.Coverage, afterInstall.CoveredUntil, afterInstall.Keys.Count));
        Assert.Equal(("b1", LicenseKeyKind.Base, KeyStatus.Active, "pro base, covered to 2027-12-31"), (afterInstall.Keys[0].KeyId, afterInstall.Keys[0].Kind, afterInstall.Keys[0].Status, afterInstall.Keys[0].Summary));
        Assert.Equal((5, 10, 15), (afterStack!.IncludedDevices, afterStack.PurchasedDevices, afterStack.Limits.Single(l => string.Equals(l.Name, "devices", StringComparison.Ordinal)).Ceiling));
        Assert.Equal("+10 devices", afterStack.Keys.Single(k => string.Equals(k.KeyId, "a1", StringComparison.Ordinal)).Summary);
        Assert.Equal(("enterprise", 2), (afterReplace!.Tier, afterReplace.Keys.Count));
        Assert.Equal(("enterprise", 0, 1), (afterRemove!.Tier, afterRemove.PurchasedDevices, afterRemove.Keys.Count));
        Assert.Equal("enterprise", app.Services.GetRequiredService<ILicense>().Current.Tier.Name);
        Assert.Equal(HttpStatusCode.OK, settingPage.StatusCode);
        Assert.DoesNotContain("payload", setting, StringComparison.Ordinal);   // the secret is masked, never returned
    }



    private static async Task<WebApplication> StartHostAsync(string connectionString, string publicKey)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Wms:Database:Provider"] = "PostgreSql",
            ["Wms:Database:ConnectionString"] = connectionString,
            ["Wms:Database:AutoMigrate"] = "true",
            ["Wms:Logging:Stdout"] = "false",
        });
        builder.UseWmsSerilog();
        builder.Services.AddWmsApiVersioning();
        builder.Services.AddWmsProblemDetails();
        builder.Services.AddWmsModules();
        builder.Services.AddWmsSettingsModule();
        builder.Services.AddWmsAuthModule();
        builder.Services.AddWmsRolesModule();
        builder.Services.AddSingleton(new LicenseVerifier(publicKey));   // the test pair stands in for the vendor's
        builder.Services.AddWmsLicenseModule();
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        var app = builder.Build();
        app.UseWmsProblemDetails();
        app.UseWmsAuth();
        app.UseWmsCorrelation();
        app.MapWmsApi().MapWmsModules();
        await app.StartAsync();
        return app;
    }



    private static StringContent Body<T>(T value)
    {
        return new StringContent(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");
    }
}
