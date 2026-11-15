// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.BreakGlass;

/// <summary>
/// The gate on a host without a database (E9.3): nothing is stored, so local sign-in stays open (and fails
/// anyway, because there are no accounts); an unlock or lock is refused with <c>auth.unavailable</c>.
/// Replaced by the stored gate when <c>AddWmsDatabase</c> runs.
/// </summary>
public sealed class NoLocalLoginGate : ILocalLoginGate
{
    /// <inheritdoc/>
    public Task<LocalLoginGateInfo> GetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(LocalLoginGateInfo.Open);
    }



    /// <inheritdoc/>
    public Task<LocalLoginGateInfo> MarkSsoVerifiedAsync(string provider, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        return Task.FromResult(LocalLoginGateInfo.Open);
    }



    /// <inheritdoc/>
    /// <exception cref="AuthException">Always: there is no database to store the window in.</exception>
    public Task<LocalLoginGateInfo> UnlockAsync(TimeSpan window, string openedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(openedBy);

        throw new AuthException(AuthErrorCodes.Unavailable, "The database is not configured; there is no gate to unlock.");
    }



    /// <inheritdoc/>
    /// <exception cref="AuthException">Always: there is no database to store the window in.</exception>
    public Task<LocalLoginGateInfo> LockAsync(string closedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(closedBy);

        throw new AuthException(AuthErrorCodes.Unavailable, "The database is not configured; there is no gate to lock.");
    }
}
