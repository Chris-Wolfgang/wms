// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Configuration;
using Wolfgang.Wms.Core.Devices;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Json;
using Wolfgang.Wms.Core.Localization;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Schema;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;

var builder = WebApplication.CreateBuilder(args);

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

// E82.7: device groups call .RequireDeviceVersion(); the minimum comes from settings once E12 lands.
builder.Services.AddWmsDeviceVersioning();

// Modules register here explicitly (ADR 0001): services.AddPickingModule() etc. No assembly scanning.
builder.Services.AddWmsModules();
builder.Services.AddWmsSchemaModule();   // E82.5: GET /system/schema, read-only
builder.Services.AddWmsSettingsModule();   // E6.1: GET /settings/registry, the settings every module declares
builder.Services.AddWmsAuthModule();   // E9: local sign-in, session cookie on the shared key ring, password change

// E6.5: appsettings holds bootstrap keys only; anything else is named in a startup warning and ignored.
builder.Services.AddWmsBootstrapConfigurationCheck();

// E8.1/E8.5: the Data Protection key ring (Wms:DataProtection:KeyRingPath, else the database ring) and the
// secret protector every encrypted value goes through.
builder.Services.AddWmsDataProtection(builder.Configuration);

// E2.1: Wms:Database:{Provider,ConnectionString,TrustServerCertificate}; an unknown provider fails startup.
builder.Services.AddWmsDatabase(builder.Configuration);

var app = builder.Build();

app.UseWmsProblemDetails();
app.UseWmsCompression();
app.UseWmsRequestLocalization();
app.UseWmsAuth();   // E9: rate limiter, authentication, authorization, must-change-password gate

app.MapGet("/", () => "Wolfgang.Wms API").AllowAnonymous();   // the product name, nothing else

// Every module endpoint lives under the versioned root; nothing is mapped on `app` directly (E82.1).
var api = app.MapWmsApi();
api.MapWmsModules();

app.Run();

/// <summary>
/// Entry point marker so integration tests can host the API with <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program
{
}
