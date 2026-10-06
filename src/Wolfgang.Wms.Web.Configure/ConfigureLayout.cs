// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Web.Shared;
using Wolfgang.Wms.Web.Shared.Components;

namespace Wolfgang.Wms.Web.Configure;

/// <summary>
/// Layout of the Configure workspace: the shared workspace chrome bound to <see cref="Workspaces.Configure"/>.
/// </summary>
public sealed class ConfigureLayout : WorkspaceLayout
{
    /// <inheritdoc/>
    protected override Workspace Workspace => Workspaces.Configure;
}
