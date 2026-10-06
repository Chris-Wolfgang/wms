// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Web.Shared;

/// <summary>
/// Whether the current user may enter a workspace, and if not, why.
/// </summary>
public enum WorkspaceAccessResult
{
    /// <summary>
    /// Licensed and permitted.
    /// </summary>
    Allowed = 0,

    /// <summary>
    /// The installation's license does not include the workspace's feature.
    /// </summary>
    NotLicensed = 1,

    /// <summary>
    /// The user's roles do not grant the workspace's permission.
    /// </summary>
    NotPermitted = 2,
}
