// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// E6.5: appsettings holds bootstrap keys only; anything else is named in a startup warning and ignored.
builder.Services.AddWmsBootstrapConfigurationCheck();

// Jobs register here as hosted services (E1.10); the worker is a host only.
var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
