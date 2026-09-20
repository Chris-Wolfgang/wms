// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// The local accounts (E9): the bootstrap administrator and any user who signs in with a password rather
/// than through a provider (E11). Passwords are hashed with a modern algorithm, failures lock the account
/// for a while, and every attempt is logged at Warning. Implemented over the database; before one exists the
/// endpoints answer <c>auth.unavailable</c>.
/// </summary>
public interface ILocalAccounts
{
    /// <summary>
    /// Checks a user name and password.
    /// </summary>
    Task<LocalLoginResult> LoginAsync(string userName, string password, CancellationToken cancellationToken);



    /// <summary>
    /// Replaces a user's password after checking the current one and the policy; clears the must-change flag.
    /// </summary>
    Task<PasswordChangeOutcome> ChangePasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken cancellationToken);



    /// <summary>
    /// The account with <paramref name="userId"/>, or null.
    /// </summary>
    Task<LocalUser?> FindAsync(long userId, CancellationToken cancellationToken);



    /// <summary>
    /// Creates the bootstrap administrator once (E9.1): when no local administrator exists yet, the account
    /// named <paramref name="userName"/> is created with <see cref="PasswordPolicy.BootstrapDefault"/> and
    /// must change it on first sign-in. Returns true when it was created now.
    /// </summary>
    Task<bool> EnsureBootstrapAdminAsync(string userName, CancellationToken cancellationToken);
}
