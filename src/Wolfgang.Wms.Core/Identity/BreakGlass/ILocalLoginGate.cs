// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.BreakGlass;

/// <summary>
/// The break-glass gate over local sign-in (E9.3): open until single sign-on is verified, closed afterwards,
/// re-opened for a timed window from the host only. Infrastructure stores it in one audited row; the
/// placeholder (<see cref="NoLocalLoginGate"/>) keeps sign-in open on a host without a database.
/// </summary>
public interface ILocalLoginGate
{
    /// <summary>
    /// The gate as stored.
    /// </summary>
    Task<LocalLoginGateInfo> GetAsync(CancellationToken cancellationToken);



    /// <summary>
    /// Records that <paramref name="provider"/> completed an external sign-in, closing local sign-in from
    /// now on. Only the first call changes anything; later calls return the stored gate.
    /// </summary>
    Task<LocalLoginGateInfo> MarkSsoVerifiedAsync(string provider, CancellationToken cancellationToken);



    /// <summary>
    /// Opens local sign-in for <paramref name="window"/> (within <see cref="LocalLoginGateRules"/>) on behalf
    /// of <paramref name="openedBy"/>, the host OS user, which the audit record keeps. A new window replaces
    /// an open one.
    /// </summary>
    Task<LocalLoginGateInfo> UnlockAsync(TimeSpan window, string openedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Closes an open window early on behalf of <paramref name="closedBy"/>; a no-op when none is open.
    /// </summary>
    Task<LocalLoginGateInfo> LockAsync(string closedBy, CancellationToken cancellationToken);
}
