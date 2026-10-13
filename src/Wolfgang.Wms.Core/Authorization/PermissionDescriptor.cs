// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// One catalog entry as the API and the console see it (E10.1).
/// </summary>
/// <param name="Name">The permission name (<c>settings.write</c>).</param>
/// <param name="Description">What it allows.</param>
/// <param name="Module">The module that declares it (<c>console</c> for workspaces).</param>
public sealed record PermissionDescriptor(string Name, string Description, string Module);
