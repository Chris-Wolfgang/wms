// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Web.Shared;
using Wolfgang.Wms.Web.Shared.Components;

namespace Wolfgang.Wms.Web.Insights;

/// <summary>
/// Layout of the Insights workspace: the shared workspace chrome bound to <see cref="Workspaces.Insights"/>.
/// </summary>
public sealed class InsightsLayout : WorkspaceLayout
{
    /// <inheritdoc/>
    protected override Workspace Workspace => Workspaces.Insights;
}
