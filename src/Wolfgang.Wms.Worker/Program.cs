// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Configuration;
using Wolfgang.Wms.Core.Licensing;
using Wolfgang.Wms.Core.Logging;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Integrity;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "WolfgangWms.Worker");   // E15.1: a no-op outside a service
builder.UseWmsSerilog();   // E12.2: the same log pipeline as the API
// E6.5: appsettings holds bootstrap keys only; anything else is named in a startup warning and ignored.
builder.Services.AddWmsBootstrapConfigurationCheck();
builder.Services.AddWmsDataProtection(builder.Configuration);   // E8.1: the same key ring as the API
builder.Services.AddWmsDatabase(builder.Configuration);   // E2.1: the worker reaches the same database

// E14.2: the role decides which jobs this instance hosts. `worker` runs the singleton jobs (under the
// leader lock); `ingest` hosts the ingest jobs only once they land and runs no singleton job.
var role = builder.Configuration["Wms:Worker:Role"] ?? "worker";
if (string.Equals(role, "worker", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddWmsIntegrityVerification();   // E10.4: re-verifies every signed security row on a schedule
}
else if (!string.Equals(role, "ingest", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException($"Wms:Worker:Role must be worker or ingest; got '{role}'.");
}

builder.Services.AddWmsLoggingModule();   // E12.4: the worker's level follows the same settings (no endpoints are mapped here)
builder.Services.AddWmsLicenseModule();   // E79: the worker's jobs read the same license (no endpoints are mapped here)

// Jobs register here as hosted services (E1.10); the worker is a host only.
var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
