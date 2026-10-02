// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using System.Security.Claims;

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// The claims a console session carries (E9, E10.5) and how a <see cref="LocalUser"/> becomes a principal.
/// The session cookie is protected by the shared Data Protection ring, so every instance reads it (E12.6).
/// </summary>
public static class SessionClaims
{
    /// <summary>
    /// The user's id (<see cref="ClaimTypes.NameIdentifier"/>).
    /// </summary>
    public const string UserId = ClaimTypes.NameIdentifier;



    /// <summary>
    /// The user name (<see cref="ClaimTypes.Name"/>).
    /// </summary>
    public const string UserName = ClaimTypes.Name;



    /// <summary>
    /// The display name.
    /// </summary>
    public const string DisplayName = "wms:display_name";



    /// <summary>
    /// <c>true</c> while the user must replace the password before anything else (E9.1).
    /// </summary>
    public const string MustChangePassword = "wms:must_change_password";



    /// <summary>
    /// <c>true</c> for the break-glass administrator.
    /// </summary>
    public const string LocalAdmin = "wms:local_admin";



    /// <summary>
    /// The authentication type recorded on local sign-ins.
    /// </summary>
    public const string LocalAuthenticationType = "local";



    /// <summary>
    /// The principal for a local sign-in.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> is null.</exception>
    public static ClaimsPrincipal Principal(LocalUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identity = new ClaimsIdentity(LocalAuthenticationType, UserName, roleType: null);
        identity.AddClaim(new Claim(UserId, user.Id.ToString(CultureInfo.InvariantCulture)));
        identity.AddClaim(new Claim(UserName, user.UserName));
        identity.AddClaim(new Claim(DisplayName, user.DisplayName));
        identity.AddClaim(new Claim(MustChangePassword, user.MustChangePassword ? "true" : "false"));
        identity.AddClaim(new Claim(LocalAdmin, user.IsLocalAdmin ? "true" : "false"));
        return new ClaimsPrincipal(identity);
    }



    /// <summary>
    /// The user id of a signed-in principal, or null when it carries none.
    /// </summary>
    public static long? UserIdOf(ClaimsPrincipal? principal)
    {
        return long.TryParse(principal?.FindFirst(UserId)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
    }



    /// <summary>
    /// True when the principal is signed in and must change the password first.
    /// </summary>
    public static bool MustChangePasswordOf(ClaimsPrincipal? principal)
    {
        return string.Equals(principal?.FindFirst(MustChangePassword)?.Value, "true", StringComparison.Ordinal);
    }
}
