// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// What a local sign-in attempt led to (E9.2).
/// </summary>
public enum LocalLoginOutcome
{
    /// <summary>The credentials matched an enabled, unlocked account.</summary>
    Success,

    /// <summary>Unknown user or wrong password (reported identically).</summary>
    InvalidCredentials,

    /// <summary>Too many failures; sign-in refused until <see cref="LocalLoginResult.LockedUntil"/>.</summary>
    LockedOut,

    /// <summary>The account is disabled.</summary>
    Disabled,
}
