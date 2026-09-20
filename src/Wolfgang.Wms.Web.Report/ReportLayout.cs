// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Web.Shared;
using Wolfgang.Wms.Web.Shared.Components;

namespace Wolfgang.Wms.Web.Report;

/// <summary>
/// Layout of the Report workspace: the shared workspace chrome bound to <see cref="Workspaces.Report"/>.
/// </summary>
public sealed class ReportLayout : WorkspaceLayout
{
    /// <inheritdoc/>
    protected override Workspace Workspace => Workspaces.Report;
}
