// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// How a provider signs a console user in (E11.0).
/// </summary>
public enum AuthProviderKind
{
    /// <summary>The user posts credentials to the API (local accounts, LDAP later).</summary>
    Credentials,

    /// <summary>The browser is sent to the provider and comes back with a token (OIDC, SAML, Windows).</summary>
    Challenge,
}
