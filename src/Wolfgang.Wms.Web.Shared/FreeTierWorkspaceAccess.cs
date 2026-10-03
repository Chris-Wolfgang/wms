// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Web.Shared;

/// <summary>
/// Placeholder access until the license (E79) and identity (E11) endpoints exist: every free-tier workspace is
/// open, paid workspaces answer <see cref="WorkspaceAccessResult.NotLicensed"/>. Replaced by the API-backed
/// implementation without touching any component.
/// </summary>
public sealed class FreeTierWorkspaceAccess : IWorkspaceAccess
{
    /// <inheritdoc/>
    public Task<WorkspaceAccessResult> CheckAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        return Task.FromResult(workspace.FreeTier ? WorkspaceAccessResult.Allowed : WorkspaceAccessResult.NotLicensed);
    }
}
