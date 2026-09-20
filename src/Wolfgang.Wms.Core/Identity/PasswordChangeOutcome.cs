// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// What a password change led to (E9.1, E9.2).
/// </summary>
public enum PasswordChangeOutcome
{
    /// <summary>The password was replaced.</summary>
    Changed,

    /// <summary>The current password did not match.</summary>
    CurrentPasswordWrong,

    /// <summary>The new password breaks <see cref="PasswordPolicy"/> (too short, or the documented default).</summary>
    NewPasswordRejected,

    /// <summary>No such account.</summary>
    UserNotFound,
}
