// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Web.Shared;

/// <summary>
/// Answers whether the current user may enter a workspace (E82.4): the license check
/// (<c>ILicense.HasFeature</c> behind the API, E79) and the permission check (E11). The console is an API
/// client, so the implementation calls the API; components never see a server service.
/// </summary>
public interface IWorkspaceAccess
{
    /// <summary>
    /// The access result for one workspace.
    /// </summary>
    Task<WorkspaceAccessResult> CheckAsync(Workspace workspace, CancellationToken cancellationToken);



    /// <summary>
    /// The workspaces the user may enter, in the given order.
    /// </summary>
    async Task<IReadOnlyList<Workspace>> EnterableAsync(IReadOnlyList<Workspace> workspaces, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspaces);

        var enterable = new List<Workspace>(workspaces.Count);
        foreach (var workspace in workspaces)
        {
            if (await CheckAsync(workspace, cancellationToken).ConfigureAwait(false) == WorkspaceAccessResult.Allowed)
            {
                enterable.Add(workspace);
            }
        }

        return enterable;
    }
}
