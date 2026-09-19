// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Localization;
using Wolfgang.Wms.Core.Modules;

var builder = WebApplication.CreateBuilder(args);

// E1.14: source-generated DataAnnotations validation for minimal-API parameters (AOT-safe), camelCase JSON,
// resource-file localization with the culture resolved per request.
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.AddWmsLocalization();

// E82.2: one API, path-versioned (/api/v0/...), one OpenAPI document per version (/openapi/v0.json).
builder.Services.AddWmsApiVersioning();

// Modules register here explicitly (ADR 0001): services.AddPickingModule() etc. No assembly scanning.
builder.Services.AddWmsModules();

var app = builder.Build();

app.UseWmsRequestLocalization();

app.MapGet("/", () => "Wolfgang.Wms API");

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
