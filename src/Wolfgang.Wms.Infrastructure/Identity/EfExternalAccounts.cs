// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Identity.External;
using Wolfgang.Wms.Core.Identity.Providers;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Integrity;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// <see cref="IExternalAccounts"/> over <c>core.user</c> (E11.1): the account is found by (provider,
/// subject) or created; its names follow the provider; its assignments are replaced by what the group
/// mappings yield (E11.2), signed like any assignment (E10.4). A name already taken by another account
/// gets <c>@provider</c> appended.
/// </summary>
public sealed partial class EfExternalAccounts : IExternalAccounts
{
    private readonly WmsDbContext _context;
    private readonly IRoles _roles;
    private readonly IIntegritySigner _signer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EfExternalAccounts> _logger;



    /// <summary>
    /// Creates the accounts.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfExternalAccounts(WmsDbContext context, IRoles roles, IIntegritySigner signer, TimeProvider timeProvider, ILogger<EfExternalAccounts> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _roles = roles ?? throw new ArgumentNullException(nameof(roles));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public async Task<ExternalSignInResult> SignInAsync(string provider, ExternalIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(identity);

        var now = _timeProvider.GetUtcNow();
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Provider == provider && u.ProviderSubject == identity.Subject, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            user = new User { Provider = provider, ProviderSubject = identity.Subject, CreatedAt = now };
            await NameAsync(user, identity, cancellationToken).ConfigureAwait(false);
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);   // the id is needed for the assignments (E10.4)
            LogCreated(_logger, user.UserName, provider);
        }
        else if (!await _signer.IsValidAsync(user, "core.user", cancellationToken).ConfigureAwait(false))
        {
            LogAttempt(_logger, identity.UserName, provider, ExternalSignInOutcome.IntegrityFailure);
            return ExternalSignInResult.Refused(ExternalSignInOutcome.IntegrityFailure);
        }

        if (user.IsDisabled)
        {
            LogAttempt(_logger, user.UserName, provider, ExternalSignInOutcome.Disabled);
            return ExternalSignInResult.Refused(ExternalSignInOutcome.Disabled);
        }

        if (!string.Equals(user.DisplayName, identity.DisplayName, StringComparison.Ordinal))
        {
            user.DisplayName = identity.DisplayName;
        }

        user.UpdatedAt = now;
        await SyncAssignmentsAsync(user, provider, identity.Groups, now, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogAttempt(_logger, user.UserName, provider, ExternalSignInOutcome.Success);

        var grants = await _roles.GrantsOfAsync(user.Id, now, cancellationToken).ConfigureAwait(false);
        return ExternalSignInResult.Succeeded(new LocalUser(user.Id, user.UserName, user.DisplayName, MustChangePassword: false, IsLocalAdmin: false, IsDisabled: false, grants));
    }



    private async Task NameAsync(User user, ExternalIdentity identity, CancellationToken cancellationToken)
    {
        var wanted = identity.UserName.Trim();
        var name = wanted.Length > 0 ? wanted : identity.Subject;
        if (await _context.Users.AnyAsync(u => u.UserNameNormalized == EfLocalAccounts.Normalize(name), cancellationToken).ConfigureAwait(false))
        {
            name = name + "@" + user.Provider;   // a local account already holds the plain name
        }

        if (name.Length > User.UserNameLength || await _context.Users.AnyAsync(u => u.UserNameNormalized == EfLocalAccounts.Normalize(name), cancellationToken).ConfigureAwait(false))
        {
            throw new AuthException(AuthErrorCodes.ProviderFailed, $"The sign-in name '{name}' cannot be used; ask an administrator to free it.");
        }

        user.UserName = name;
        user.UserNameNormalized = EfLocalAccounts.Normalize(name);
        user.DisplayName = identity.DisplayName.Length > 0 ? identity.DisplayName : name;
    }



    /// <summary>
    /// E11.2: the assignments become exactly what the mappings of the user's groups yield.
    /// </summary>
    private async Task SyncAssignmentsAsync(User user, string provider, IReadOnlyList<string> groups, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var wanted = groups.Count == 0
            ? []
            : await _context.GroupRoleMappings.AsNoTracking()
                .Where(m => m.Provider == provider && groups.Contains(m.GroupKey))
                .Select(m => new { m.RoleId, m.SiteId })
                .Distinct()
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        var current = await _context.UserRoles.Where(a => a.UserId == user.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var changed = false;
        foreach (var stale in current.Where(a => !wanted.Any(w => w.RoleId == a.RoleId && w.SiteId == a.SiteId)))
        {
            _context.UserRoles.Remove(stale);
            changed = true;
        }

        foreach (var missing in wanted.Where(w => !current.Any(a => a.RoleId == w.RoleId && a.SiteId == w.SiteId)))
        {
            _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = missing.RoleId, SiteId = missing.SiteId, UpdatedAt = now, UpdatedBy = provider });
            changed = true;
        }

        if (changed)
        {
            user.SessionsValidAfter = now;   // E10.5: other sessions of this user re-read their grants
            LogRolesSynced(_logger, user.UserName, wanted.Count);
        }
    }



    [LoggerMessage(Level = LogLevel.Information, Message = "Provider account '{UserName}' created from '{Provider}'.")]
    private static partial void LogCreated(ILogger logger, string userName, string provider);



    [LoggerMessage(Level = LogLevel.Warning, Message = "Provider sign-in for '{UserName}' via '{Provider}': {Outcome}.")]
    private static partial void LogAttempt(ILogger logger, string userName, string provider, ExternalSignInOutcome outcome);



    [LoggerMessage(Level = LogLevel.Information, Message = "Provider account '{UserName}': assignments replaced by {Count} mapped roles.")]
    private static partial void LogRolesSynced(ILogger logger, string userName, int count);
}
