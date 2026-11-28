// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Identity.Providers;

namespace Wolfgang.Wms.Core.Identity.External;

/// <summary>
/// The console accounts of provider users (E11.1): one <c>core.user</c> row per (provider, subject),
/// created on first sign-in and refreshed on every sign-in; their roles come from the provider's group
/// mappings (E11.2) and nowhere else, so a user in no mapped group holds no role.
/// </summary>
public interface IExternalAccounts
{
    /// <summary>
    /// Finds or creates the account for <paramref name="identity"/>, refreshes its names, replaces its
    /// role assignments with the ones the group mappings yield, and returns the session user.
    /// </summary>
    Task<ExternalSignInResult> SignInAsync(string provider, ExternalIdentity identity, CancellationToken cancellationToken);
}
