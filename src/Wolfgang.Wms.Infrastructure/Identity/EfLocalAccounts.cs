// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// <see cref="ILocalAccounts"/> over <c>core.user</c> (E9): passwords hashed with ASP.NET Core Identity's
/// PBKDF2 hasher, lockout after the configured number of failures for the configured duration, every attempt
/// logged at Warning with its outcome (E10.6), and the bootstrap administrator created once (E9.1).
/// </summary>
public sealed partial class EfLocalAccounts : ILocalAccounts
{
    private readonly WmsDbContext _context;
    private readonly IPasswordHasher<User> _hasher;
    private readonly ISettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EfLocalAccounts> _logger;



    /// <summary>
    /// Creates the accounts.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfLocalAccounts(WmsDbContext context, IPasswordHasher<User> hasher, ISettings settings, TimeProvider timeProvider, ILogger<EfLocalAccounts> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <inheritdoc/>
    public async Task<LocalLoginResult> LoginAsync(string userName, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(password);

        var now = _timeProvider.GetUtcNow();
        var user = await FindByNameAsync(userName, cancellationToken).ConfigureAwait(false);
        if (user?.PasswordHash is null)
        {
            _hasher.VerifyHashedPassword(new User(), _hasher.HashPassword(new User(), "dummy"), password);   // same cost whether the name exists or not
            LogAttempt(_logger, userName, LocalLoginOutcome.InvalidCredentials);
            return LocalLoginResult.Refused(LocalLoginOutcome.InvalidCredentials);
        }

        if (user.IsDisabled)
        {
            LogAttempt(_logger, userName, LocalLoginOutcome.Disabled);
            return LocalLoginResult.Refused(LocalLoginOutcome.Disabled);
        }

        if (user.LockedUntil is { } lockedUntil && lockedUntil > now)
        {
            LogAttempt(_logger, userName, LocalLoginOutcome.LockedOut);
            return LocalLoginResult.Refused(LocalLoginOutcome.LockedOut, lockedUntil);
        }

        var verification = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return await RecordFailureAsync(user, now, cancellationToken).ConfigureAwait(false);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogAttempt(_logger, userName, LocalLoginOutcome.Success);
        return LocalLoginResult.Succeeded(View(user));
    }



    /// <inheritdoc/>
    public async Task<PasswordChangeOutcome> ChangePasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentPassword);
        ArgumentNullException.ThrowIfNull(newPassword);

        var user = await _context.Set<User>().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user?.PasswordHash is null)
        {
            return PasswordChangeOutcome.UserNotFound;
        }

        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            LogPasswordChange(_logger, user.UserName, PasswordChangeOutcome.CurrentPasswordWrong);
            return PasswordChangeOutcome.CurrentPasswordWrong;
        }

        if (PasswordPolicy.Check(newPassword) is not null)
        {
            LogPasswordChange(_logger, user.UserName, PasswordChangeOutcome.NewPasswordRejected);
            return PasswordChangeOutcome.NewPasswordRejected;
        }

        var now = _timeProvider.GetUtcNow();
        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        user.SessionsValidAfter = now;
        user.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogPasswordChange(_logger, user.UserName, PasswordChangeOutcome.Changed);
        return PasswordChangeOutcome.Changed;
    }



    /// <inheritdoc/>
    public async Task<LocalUser?> FindAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>().AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        return user is null ? null : View(user);
    }



    /// <inheritdoc/>
    public async Task<bool> EnsureBootstrapAdminAsync(string userName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        if (await _context.Set<User>().AnyAsync(u => u.IsLocalAdmin, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        var admin = new User
        {
            UserName = userName,
            UserNameNormalized = Normalize(userName),
            DisplayName = "Administrator",
            MustChangePassword = true,
            IsLocalAdmin = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        admin.PasswordHash = _hasher.HashPassword(admin, PasswordPolicy.BootstrapDefault);
        _context.Set<User>().Add(admin);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogBootstrapAdmin(_logger, userName);
        return true;
    }



    /// <summary>
    /// The normalised form of a user name (invariant upper case), the unique sign-in key.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="userName"/> is null.</exception>
    public static string Normalize(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);
        return userName.Trim().ToUpperInvariant();
    }



    private async Task<LocalLoginResult> RecordFailureAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var threshold = await _settings.GetAsync(AuthSettings.LockoutThreshold, SettingScopeRef.Organization, cancellationToken).ConfigureAwait(false);
        user.FailedLoginCount++;
        user.UpdatedAt = now;
        var outcome = LocalLoginOutcome.InvalidCredentials;
        if (user.FailedLoginCount >= threshold)
        {
            user.LockedUntil = now + await _settings.GetAsync(AuthSettings.LockoutDuration, SettingScopeRef.Organization, cancellationToken).ConfigureAwait(false);
            user.FailedLoginCount = 0;
            outcome = LocalLoginOutcome.LockedOut;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogAttempt(_logger, user.UserName, outcome);
        return LocalLoginResult.Refused(outcome, user.LockedUntil);
    }



    private Task<User?> FindByNameAsync(string userName, CancellationToken cancellationToken)
    {
        var normalized = Normalize(userName);
        return _context.Set<User>().SingleOrDefaultAsync(u => u.UserNameNormalized == normalized, cancellationToken);
    }



    private static LocalUser View(User user)
    {
        // E10.1: the local administrator holds every permission everywhere; other users' grants come from their roles (E10.2).
        IReadOnlyList<string> grants = user.IsLocalAdmin ? [PermissionClaims.OrganizationGrant(PermissionClaims.Wildcard)] : [];
        return new LocalUser(user.Id, user.UserName, user.DisplayName, user.MustChangePassword, user.IsLocalAdmin, user.IsDisabled, grants);
    }



    [LoggerMessage(Level = LogLevel.Warning, Message = "Local sign-in for '{UserName}': {Outcome}.")]
    private static partial void LogAttempt(ILogger logger, string userName, LocalLoginOutcome outcome);



    [LoggerMessage(Level = LogLevel.Warning, Message = "Password change for '{UserName}': {Outcome}.")]
    private static partial void LogPasswordChange(ILogger logger, string userName, PasswordChangeOutcome outcome);



    [LoggerMessage(Level = LogLevel.Information, Message = "Bootstrap administrator '{UserName}' created; sign in with the documented default password and change it (docs/AUTH.md).")]
    private static partial void LogBootstrapAdmin(ILogger logger, string userName);
}
