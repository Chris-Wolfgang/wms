// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Modules;

var builder = WebApplication.CreateBuilder(args);

// Modules register here explicitly (ADR 0001): services.AddPickingModule() etc. No assembly scanning.
builder.Services.AddWmsModules();

var app = builder.Build();

app.MapGet("/", () => "Wolfgang.Wms API");
app.MapWmsModules();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Entry point marker so integration tests can host the API with <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program
{
}
