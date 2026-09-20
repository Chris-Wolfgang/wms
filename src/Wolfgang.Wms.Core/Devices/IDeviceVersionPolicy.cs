// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Devices;

/// <summary>
/// The minimum app version the server accepts from handhelds (E82.7). The value is a setting cascading
/// organisation → site (E12), so old builds can be retired one warehouse at a time while the server runs
/// one version for all; until the settings module exists, <see cref="NoMinimumDeviceVersionPolicy"/> accepts
/// every version.
/// </summary>
public interface IDeviceVersionPolicy
{
    /// <summary>
    /// The minimum version for the calling device's site, or null when none is configured.
    /// </summary>
    Task<Version?> GetMinimumAsync(CancellationToken cancellationToken);
}
