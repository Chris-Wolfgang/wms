// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// What a local password must satisfy (E9.1, E9.2): a minimum length, and never the documented bootstrap
/// default, which exists only so an installer can sign in once and replace it.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>
    /// The password the bootstrap administrator is created with (docs/AUTH.md). Documented, not secret:
    /// the first sign-in must replace it before anything else is reachable.
    /// </summary>
    public const string BootstrapDefault = "ChangeMe-2026!";



    /// <summary>
    /// Shortest password accepted.
    /// </summary>
    public const int MinimumLength = 12;



    /// <summary>
    /// The reason a new password is refused, or null when it is acceptable.
    /// </summary>
    public static string? Check(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
        {
            return $"The password must be at least {MinimumLength} characters.";
        }

        if (string.Equals(password, BootstrapDefault, StringComparison.Ordinal))
        {
            return "The documented default password cannot be used; choose your own.";
        }

        return null;
    }
}
