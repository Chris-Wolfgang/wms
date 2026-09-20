// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// <c>Wms:Bootstrap</c> (E9.1): what the first run creates. Read once; ignored once a local administrator
/// exists.
/// </summary>
public sealed class BootstrapOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public const string SectionName = "Wms:Bootstrap";



    /// <summary>
    /// The user name of the bootstrap administrator.
    /// </summary>
    public string AdminUserName { get; set; } = "admin";
}
