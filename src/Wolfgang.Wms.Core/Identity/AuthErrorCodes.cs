// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// Error codes of the <c>auth</c> module (E9).
/// </summary>
public static class AuthErrorCodes
{
    /// <summary>
    /// Unknown user or wrong password; reported identically so names cannot be probed.
    /// </summary>
    public static ErrorCode InvalidCredentials { get; } = new("auth.invalid_credentials", StatusCodes.Status401Unauthorized, "The user name or password is wrong.", "auth-invalid-credentials", ErrorSeverity.Warning);



    /// <summary>
    /// Too many failures; the account is locked for a while.
    /// </summary>
    public static ErrorCode LockedOut { get; } = new("auth.locked_out", StatusCodes.Status423Locked, "The account is locked until {0}.", "auth-locked-out", ErrorSeverity.Warning);



    /// <summary>
    /// The account is disabled.
    /// </summary>
    public static ErrorCode Disabled { get; } = new("auth.disabled", StatusCodes.Status403Forbidden, "The account is disabled.", "auth-disabled", ErrorSeverity.Warning);



    /// <summary>
    /// The account row does not match its integrity signature (E10.4): it was changed outside the
    /// application and is not honoured until an administrator repairs it.
    /// </summary>
    public static ErrorCode IntegrityFailure { get; } = new("auth.integrity_failure", StatusCodes.Status403Forbidden, "The account cannot be verified; contact an administrator.", "auth-integrity-failure", ErrorSeverity.Error);



    /// <summary>
    /// The request needs a signed-in user.
    /// </summary>
    public static ErrorCode NotSignedIn { get; } = new("auth.not_signed_in", StatusCodes.Status401Unauthorized, "Sign in first.", "auth-not-signed-in", ErrorSeverity.Info);



    /// <summary>
    /// The signed-in user lacks the right to do this.
    /// </summary>
    public static ErrorCode Forbidden { get; } = new("auth.forbidden", StatusCodes.Status403Forbidden, "You are not allowed to do this.", "auth-forbidden", ErrorSeverity.Warning);



    /// <summary>
    /// The user must replace the bootstrap or reset password before anything else (E9.1).
    /// </summary>
    public static ErrorCode PasswordChangeRequired { get; } = new("auth.password_change_required", StatusCodes.Status403Forbidden, "Change your password before continuing.", "auth-password-change-required", ErrorSeverity.Warning);



    /// <summary>
    /// A password change was refused: wrong current password or a new one the policy rejects.
    /// </summary>
    public static ErrorCode PasswordRejected { get; } = new("auth.password_rejected", StatusCodes.Status400BadRequest, "{0}", "auth-password-rejected", ErrorSeverity.Error);



    /// <summary>
    /// The named identity provider is not enabled (or not registered) on this host (E11.0).
    /// </summary>
    public static ErrorCode ProviderNotEnabled { get; } = new("auth.provider_not_enabled", StatusCodes.Status404NotFound, "Identity provider '{0}' is not enabled.", "auth-provider-not-enabled", ErrorSeverity.Warning);



    /// <summary>
    /// The identity provider refused or failed the sign-in (E11.1): the detail names the reason, never a secret.
    /// </summary>
    public static ErrorCode ProviderFailed { get; } = new("auth.provider_failed", StatusCodes.Status502BadGateway, "The identity provider could not complete the sign-in: {0}", "auth-provider-failed", ErrorSeverity.Error);



    /// <summary>
    /// A group mapping was refused (E11.2): blank group, or the same mapping exists.
    /// </summary>
    public static ErrorCode MappingRejected { get; } = new("auth.mapping_rejected", StatusCodes.Status409Conflict, "{0}", "auth-mapping-rejected", ErrorSeverity.Error);



    /// <summary>
    /// No group mapping has that id (E11.2).
    /// </summary>
    public static ErrorCode MappingNotFound { get; } = new("auth.mapping_not_found", StatusCodes.Status404NotFound, "Mapping {0} does not exist.", "auth-mapping-not-found", ErrorSeverity.Error);



    /// <summary>
    /// Sign-in is unavailable until the database is configured.
    /// </summary>
    public static ErrorCode Unavailable { get; } = new("auth.unavailable", StatusCodes.Status503ServiceUnavailable, "Sign-in is unavailable until the database is configured.", "auth-unavailable", ErrorSeverity.Error);
}
