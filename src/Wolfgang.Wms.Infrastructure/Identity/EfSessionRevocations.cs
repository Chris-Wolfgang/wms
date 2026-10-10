// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// <see cref="ISessionRevocations"/> over <c>core.user</c> (E10.5): one primary-key read per request that
/// presents a session; a disabled or deleted user invalidates every session.
/// </summary>
public sealed class EfSessionRevocations : ISessionRevocations
{
    private readonly WmsDbContext _context;
    private readonly TimeProvider _timeProvider;



    /// <summary>
    /// Creates the revocations.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfSessionRevocations(WmsDbContext context, TimeProvider timeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }



    /// <inheritdoc/>
    public async Task<DateTimeOffset?> SessionsValidAfterAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.IsDisabled, u.SessionsValidAfter }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return user is null || user.IsDisabled ? DateTimeOffset.MaxValue : user.SessionsValidAfter;
    }



    /// <inheritdoc/>
    public async Task RevokeSessionsAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        user.SessionsValidAfter = now;
        user.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
