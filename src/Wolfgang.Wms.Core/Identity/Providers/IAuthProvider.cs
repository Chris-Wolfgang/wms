// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// A console identity provider (E11.0). Implementations are registered in DI at startup, one per project
/// (<c>local</c> in Core, <c>oidc</c> in <c>Wolfgang.Wms.Auth.Oidc</c>); which of them are active is the
/// <c>auth.providers.enabled</c> setting, applied at runtime by <see cref="AuthProviderState"/> without a
/// restart. Credential providers validate a posted credential through their own endpoint; challenge
/// providers also implement <see cref="IChallengeAuthProvider"/>.
/// </summary>
public interface IAuthProvider
{
    /// <summary>
    /// The provider name (<c>local</c>, <c>oidc</c>): lower-case letters, digits and dashes, unique per host,
    /// the value used in <c>auth.providers.enabled</c> and in the challenge route.
    /// </summary>
    string Name { get; }



    /// <summary>
    /// What the login page shows for it.
    /// </summary>
    string DisplayName { get; }



    /// <summary>
    /// How it signs in.
    /// </summary>
    AuthProviderKind Kind { get; }



    /// <summary>
    /// The provider's settings schema: the keys the console renders as its configuration block. Declared by
    /// the auth module so they are registered, validated and audited like any other setting.
    /// </summary>
    IReadOnlyList<SettingKey> Settings { get; }



    /// <summary>
    /// The health check ("test connection"): discovery for OIDC, a bind for LDAP, the database for local.
    /// Never throws for a failing provider; the result carries the reason.
    /// </summary>
    Task<AuthProviderHealth> CheckAsync(IServiceProvider services, CancellationToken cancellationToken);
}
