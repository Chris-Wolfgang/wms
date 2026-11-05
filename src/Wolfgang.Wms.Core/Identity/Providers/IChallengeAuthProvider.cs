// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// A provider the browser is sent to (E11.0): its ASP.NET authentication handler is added as a scheme named
/// after the provider when it is enabled and removed when it is not, so enabling needs no restart. The
/// handler signs the external principal into the session cookie; <see cref="Identify"/> turns the claims
/// into the WMS identity (E11.2 maps the groups to roles).
/// </summary>
public interface IChallengeAuthProvider : IAuthProvider
{
    /// <summary>
    /// The handler type registered as the scheme.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type HandlerType { get; }



    /// <summary>
    /// The WMS identity in the provider's claims: the stable subject, the sign-in name, the display name
    /// and the group identifiers used for role mapping.
    /// </summary>
    ExternalIdentity Identify(ClaimsPrincipal principal);



    /// <summary>
    /// Called when the provider's settings changed (or on first enablement): drop cached handler options so
    /// the next request reads the new values.
    /// </summary>
    Task ApplyAsync(IServiceProvider services, CancellationToken cancellationToken);
}
