// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Web.Shared;

/// <summary>
/// What the console shows about the break-glass gate (E9.3): a notice while a host-opened window keeps
/// local sign-in open, nothing otherwise. Read once per workspace load, so the banner appears on the next
/// navigation after an unlock and disappears on the next one after the window ends.
/// </summary>
public interface ILocalLoginNotice
{
    /// <summary>
    /// The open window, or null when local sign-in is not unlocked by a window (closed, or open for another
    /// reason such as SSO not yet verified) or when the status cannot be read.
    /// </summary>
    Task<LocalLoginNotice?> GetAsync(CancellationToken cancellationToken);
}
