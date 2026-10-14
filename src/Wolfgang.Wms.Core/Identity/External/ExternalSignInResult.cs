// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.External;

/// <summary>
/// The result of a provider sign-in (E11.1): the session user on success.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="User">The session user on success, else null.</param>
public sealed record ExternalSignInResult(ExternalSignInOutcome Outcome, LocalUser? User)
{
    /// <summary>
    /// A successful sign-in.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> is null.</exception>
    public static ExternalSignInResult Succeeded(LocalUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new ExternalSignInResult(ExternalSignInOutcome.Success, user);
    }



    /// <summary>
    /// A refused sign-in.
    /// </summary>
    public static ExternalSignInResult Refused(ExternalSignInOutcome outcome)
    {
        return new ExternalSignInResult(outcome, User: null);
    }
}
