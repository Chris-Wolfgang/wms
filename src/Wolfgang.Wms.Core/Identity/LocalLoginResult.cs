// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// The result of a local sign-in attempt (E9.2).
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="User">The account on success, else null.</param>
/// <param name="LockedUntil">When a locked account may try again, else null.</param>
public sealed record LocalLoginResult(LocalLoginOutcome Outcome, LocalUser? User, DateTimeOffset? LockedUntil)
{
    /// <summary>
    /// A successful sign-in.
    /// </summary>
    public static LocalLoginResult Succeeded(LocalUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new LocalLoginResult(LocalLoginOutcome.Success, user, LockedUntil: null);
    }



    /// <summary>
    /// A refused sign-in.
    /// </summary>
    public static LocalLoginResult Refused(LocalLoginOutcome outcome, DateTimeOffset? lockedUntil = null)
    {
        return new LocalLoginResult(outcome, User: null, lockedUntil);
    }
}
