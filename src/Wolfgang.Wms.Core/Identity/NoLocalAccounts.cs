// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// The accounts before a database is configured (bootstrap): every operation answers
/// <c>auth.unavailable</c>. <c>AddWmsDatabase</c> replaces it with the stored accounts.
/// </summary>
public sealed class NoLocalAccounts : ILocalAccounts
{
    /// <inheritdoc/>
    public Task<LocalLoginResult> LoginAsync(string userName, string password, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<PasswordChangeOutcome> ChangePasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<LocalUser?> FindAsync(long userId, CancellationToken cancellationToken)
    {
        return Task.FromResult<LocalUser?>(null);
    }



    /// <inheritdoc/>
    public Task<bool> EnsureBootstrapAdminAsync(string userName, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }



    private static AuthException Unavailable()
    {
        return new AuthException(AuthErrorCodes.Unavailable, "Sign-in is unavailable until Wms:Database is configured and the schema installed.");
    }
}
