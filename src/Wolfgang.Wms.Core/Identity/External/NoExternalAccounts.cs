// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Identity.Providers;

namespace Wolfgang.Wms.Core.Identity.External;

/// <summary>
/// The accounts of a host without a database (E11.1): every sign-in is unavailable.
/// </summary>
public sealed class NoExternalAccounts : IExternalAccounts
{
    /// <inheritdoc/>
    /// <exception cref="AuthException">Always: <see cref="AuthErrorCodes.Unavailable"/>.</exception>
    public Task<ExternalSignInResult> SignInAsync(string provider, ExternalIdentity identity, CancellationToken cancellationToken)
    {
        throw new AuthException(AuthErrorCodes.Unavailable, "Sign-in is unavailable until the database is configured.");
    }
}
