// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// A provider the user posts a user name and password to (E11.0): <c>local</c> today, LDAP later. The sign-in
/// endpoint validates through this contract, so a new credential provider plugs in without a bespoke endpoint;
/// challenge providers implement <see cref="IChallengeAuthProvider"/> instead.
/// </summary>
public interface ICredentialAuthProvider : IAuthProvider
{
    /// <summary>
    /// Checks the credentials and answers the outcome: the signed-in user on success, otherwise why not
    /// (wrong credentials, locked out, disabled, integrity failure). Records failures as the provider requires
    /// (lockout counting) and never throws for a wrong credential.
    /// </summary>
    /// <param name="userName">The name the user typed.</param>
    /// <param name="password">The password the user typed.</param>
    /// <param name="services">The request's services, for the provider's stores.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    Task<LocalLoginResult> ValidateAsync(string userName, string password, IServiceProvider services, CancellationToken cancellationToken);
}
