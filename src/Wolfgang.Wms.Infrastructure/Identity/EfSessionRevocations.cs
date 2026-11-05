// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Integrity;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// <see cref="ISessionRevocations"/> over <c>core.user</c> (E10.5): one primary-key read per request that
/// presents a session; a disabled or deleted user invalidates every session.
/// </summary>
public sealed class EfSessionRevocations : ISessionRevocations
{
    private readonly WmsDbContext _context;
    private readonly IIntegritySigner _signer;
    private readonly TimeProvider _timeProvider;



    /// <summary>
    /// Creates the revocations.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfSessionRevocations(WmsDbContext context, IIntegritySigner signer, TimeProvider timeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }



    /// <inheritdoc/>
    public async Task<DateTimeOffset?> SessionsValidAfterAsync(long userId, CancellationToken cancellationToken)
    {
        // E10.4: the row is verified on every request, not only at sign-in, so a database edit can neither keep a
        // disabled user's session alive nor move sessions-valid-after back; a row that fails ends the session.
        var user = await _context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null || !await _signer.IsValidAsync(user, UserConfiguration.Schema + "." + UserConfiguration.Table, cancellationToken).ConfigureAwait(false))
        {
            return DateTimeOffset.MaxValue;
        }

        return user.IsDisabled ? DateTimeOffset.MaxValue : user.SessionsValidAfter;
    }



    /// <inheritdoc/>
    public async Task RevokeSessionsAsync(long userId, CancellationToken cancellationToken)
    {
        // A row that fails verification is left as it is: saving it would re-sign the change made in the database
        // (E10.4), and its sessions are already refused on every request (SessionsValidAfterAsync).
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null || !await _signer.IsValidAsync(user, UserConfiguration.Schema + "." + UserConfiguration.Table, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        user.SessionsValidAfter = now;
        user.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
