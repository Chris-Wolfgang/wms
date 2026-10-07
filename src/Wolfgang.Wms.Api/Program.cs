// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Auth.Oidc;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Configuration;
using Wolfgang.Wms.Core.Hosting;
using Wolfgang.Wms.Core.Devices;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;
using Wolfgang.Wms.Core.Json;
using Wolfgang.Wms.Core.Licensing;
using Wolfgang.Wms.Core.Localization;
using Wolfgang.Wms.Core.Logging;
using Wolfgang.Wms.Logging;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Organization;
using Wolfgang.Wms.Core.Schema;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Core.Zones;
using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Core.Imports;

var builder = WebApplication.CreateBuilder(args);

// E15.1: as a Windows service the host reports to the service controller and roots itself at the executable;
// under IIS (in-process, E15.2) or on a console this changes nothing.
builder.Host.UseWindowsService(options => options.ServiceName = "WolfgangWms.Api");

// E12.2: Serilog from Wms:Logging (stdout JSON, file, Event Log, OpenTelemetry); redacted; level switch.
builder.UseWmsSerilog();

// E1.14: source-generated DataAnnotations validation for minimal-API parameters (AOT-safe), camelCase JSON,
// resource-file localization with the culture resolved per request.
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    // E1.9: API records serialise through the source-generated context, never reflection.
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, WmsJsonContext.Default);
});
builder.Services.AddWmsLocalization();

// E82.2: one API, path-versioned (/api/v0/...), one OpenAPI document per version (/openapi/v0.json).
builder.Services.AddWmsApiVersioning();

// E82.3: problem-details errors with codes; Brotli/gzip responses, compressed requests accepted.
builder.Services.AddWmsProblemDetails();
builder.Services.AddWmsCompression();

// E10.6: X-Forwarded-* honoured only when Wms:Hosting:BehindProxy says a proxy fronts the host; browser
// origins allowed by the api.cors.allowed_origins setting, applied without a restart.
builder.Services.AddWmsForwardedHeaders(builder.Configuration);
builder.Services.AddWmsCors();
builder.Services.AddWmsHealth();   // E12.1: /health/live and /health/ready; the database adds its readiness check
builder.Services.AddWmsLoggingModule();   // E12.4: GET/POST/DELETE /system/logging(/elevate); the level follows the settings

// Modules register here explicitly (ADR 0001): services.AddPickingModule() etc. No assembly scanning.
builder.Services.AddWmsModules();
builder.Services.AddWmsSchemaModule();   // E82.5: GET /system/schema, read-only
builder.Services.AddWmsSettingsModule();   // E6.1: GET /settings/registry, the settings every module declares
builder.Services.AddWmsAuthModule();   // E9: local sign-in, session cookie on the shared key ring, password change
builder.Services.AddWmsOidcProvider();   // E11.1: the oidc provider, offered when auth.providers.enabled names it
builder.Services.AddWmsRolesModule();   // E10.2/E10.3: roles from the catalog, assignments everywhere or per site
builder.Services.AddWmsLicenseModule();   // E79: the free tier compiled in, pasted keys verified offline, limits enforced through the gate
builder.Services.AddWmsOrganizationModule();   // E16.0: the one organization per install, the top of the settings cascade
builder.Services.AddWmsSitesModule();   // E16.1: the warehouses, the operational level everything below the organization is scoped to
builder.Services.AddWmsZonesModule();   // E16.2: the zones within each site
builder.Services.AddWmsLocationsModule();   // E17.1: the bins within each zone
builder.Services.AddWmsImportsModule();   // E16.6: the master data import contract

// E6.5: appsettings holds bootstrap keys only; anything else is named in a startup warning and ignored.
builder.Services.AddWmsBootstrapConfigurationCheck();

// E8.1/E8.5: the Data Protection key ring (Wms:DataProtection:KeyRingPath, else the database ring) and the
// secret protector every encrypted value goes through.
builder.Services.AddWmsDataProtection(builder.Configuration);

// E2.1: Wms:Database:{Provider,ConnectionString,TrustServerCertificate}; an unknown provider fails startup.
builder.Services.AddWmsDatabase(builder.Configuration);

// E9.3: the host-only pipe wms-admin uses to open or close the break-glass gate; after the ring and the database.
builder.Services.AddWmsAdminChannel(builder.Configuration);

// E82.7: device groups call .RequireDeviceVersion(). After the modules, so the no-minimum fallback only fills
// the gap: the settings module (E12) registers the organisation -> site policy, with Add or TryAdd.
builder.Services.AddWmsDeviceVersioning();

var app = builder.Build();

app.UseWmsForwardedHeaders();   // first: everything after sees the forwarded scheme and address
app.UseWmsProblemDetails();
app.UseWmsHttpsRequired(builder.Configuration);   // E12.5: plain HTTP from the network is a 400, never a redirect
app.UseWmsCors();
app.UseWmsCompression();
app.UseWmsRequestLocalization();
app.UseWmsAuth();   // E9: rate limiter, authentication, authorization, must-change-password gate
app.UseWmsCorrelation();   // E12.3: trace id, user, device and route identifiers on every log line of the request

app.MapGet("/", () => "Wolfgang.Wms API").AllowAnonymous();   // the product name, nothing else
app.MapWmsHealth();   // E12.1: outside the versioned root, anonymous, plain HTTP allowed

// Every module endpoint lives under the versioned root; nothing is mapped on `app` directly (E82.1).
var api = app.MapWmsApi();
api.MapWmsModules();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Entry point marker so integration tests can host the API with <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program
{
}
