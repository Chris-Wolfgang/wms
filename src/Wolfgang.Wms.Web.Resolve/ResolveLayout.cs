// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Web.Shared;
using Wolfgang.Wms.Web.Shared.Components;

namespace Wolfgang.Wms.Web.Resolve;

/// <summary>
/// Layout of the Resolve workspace: the shared workspace chrome bound to <see cref="Workspaces.Resolve"/>.
/// </summary>
public sealed class ResolveLayout : WorkspaceLayout
{
    /// <inheritdoc/>
    protected override Workspace Workspace => Workspaces.Resolve;
}
