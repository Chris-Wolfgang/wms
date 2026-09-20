// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.AuditTrail;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// One row of <c>core.user</c> (E9): a console user. Local accounts carry a password hash (never audited);
/// provider accounts (E11) carry none. Audited (E6.4), versioned (E5.1), signed later (E10.4).
/// </summary>
public sealed class User : IVersionedEntity
{
    /// <summary>Longest user name.</summary>
    public const int UserNameLength = 256;

    /// <summary>Longest display name.</summary>
    public const int DisplayNameLength = 256;



    /// <summary>The server-assigned identifier.</summary>
    public long Id { get; set; }



    /// <summary>The sign-in name as entered.</summary>
    public string UserName { get; set; } = string.Empty;



    /// <summary>The sign-in name upper-cased invariantly, unique: sign-in is case-insensitive.</summary>
    public string UserNameNormalized { get; set; } = string.Empty;



    /// <summary>The name shown on screen.</summary>
    public string DisplayName { get; set; } = string.Empty;



    /// <summary>The password hash of a local account (PBKDF2, ASP.NET Core Identity format), or null for a provider account.</summary>
    [NotAudited]
    public string? PasswordHash { get; set; }



    /// <summary>True until the user has replaced the bootstrap or reset password (E9.1).</summary>
    public bool MustChangePassword { get; set; }



    /// <summary>True when sign-in is refused.</summary>
    public bool IsDisabled { get; set; }



    /// <summary>True for the break-glass administrator (E9.3): can be disabled, never deleted.</summary>
    public bool IsLocalAdmin { get; set; }



    /// <summary>Consecutive failed sign-ins since the last success.</summary>
    public int FailedLoginCount { get; set; }



    /// <summary>Until when sign-in is refused after too many failures, or null.</summary>
    public DateTimeOffset? LockedUntil { get; set; }



    /// <summary>Sessions and tokens issued before this instant are invalid (E10.5): set on password change, disable and role change.</summary>
    public DateTimeOffset? SessionsValidAfter { get; set; }



    /// <summary>When the account was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }



    /// <summary>When the account was last written (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }



    /// <inheritdoc/>
    public long RowVersion { get; set; }
}
