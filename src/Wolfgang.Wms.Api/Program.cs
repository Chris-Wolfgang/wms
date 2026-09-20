// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Devices;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Json;
using Wolfgang.Wms.Core.Localization;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Schema;

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

var app = builder.Build();

app.UseWmsProblemDetails();
app.UseWmsCompression();
app.UseWmsRequestLocalization();

app.MapGet("/", () => "Wolfgang.Wms API");

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
