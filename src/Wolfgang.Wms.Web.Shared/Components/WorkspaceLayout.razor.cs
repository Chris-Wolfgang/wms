// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Web.Shared.Components;

/// <summary>
/// The chrome every workspace shares (E82.4): product name, navigation to the workspaces the user may enter,
/// and the entry gate — the body renders only when <see cref="IWorkspaceAccess"/> allows the workspace,
/// otherwise a not-licensed or not-permitted page. Each workspace derives a layout that names its
/// <see cref="Workspace"/>.
/// </summary>
public abstract partial class WorkspaceLayout
{
    /// <summary>
    /// The workspace this layout belongs to.
    /// </summary>
    protected abstract Workspace Workspace { get; }
}
