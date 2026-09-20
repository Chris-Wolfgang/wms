// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Configuration;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Integrity;
using Wolfgang.Wms.Infrastructure.Secrets;

var builder = Host.CreateApplicationBuilder(args);

// E6.5: appsettings holds bootstrap keys only; anything else is named in a startup warning and ignored.
builder.Services.AddWmsBootstrapConfigurationCheck();
builder.Services.AddWmsDataProtection(builder.Configuration);   // E8.1: the same key ring as the API
builder.Services.AddWmsDatabase(builder.Configuration);   // E2.1: the worker reaches the same database
builder.Services.AddWmsIntegrityVerification();   // E10.4: re-verifies every signed security row on a schedule

// Jobs register here as hosted services (E1.10); the worker is a host only.
var host = builder.Build();
host.Run();
