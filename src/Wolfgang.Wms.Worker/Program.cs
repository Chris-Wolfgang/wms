// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

var builder = Host.CreateApplicationBuilder(args);

// Jobs register here as hosted services (E1.10); the worker is a host only.
var host = builder.Build();
host.Run();
