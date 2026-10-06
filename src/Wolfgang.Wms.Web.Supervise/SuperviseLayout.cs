// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Web.Shared;
using Wolfgang.Wms.Web.Shared.Components;

namespace Wolfgang.Wms.Web.Supervise;

/// <summary>
/// Layout of the Supervise workspace: the shared workspace chrome bound to <see cref="Workspaces.Supervise"/>.
/// </summary>
public sealed class SuperviseLayout : WorkspaceLayout
{
    /// <inheritdoc/>
    protected override Workspace Workspace => Workspaces.Supervise;
}
