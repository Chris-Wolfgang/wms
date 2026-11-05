// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Identity;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// <see cref="ISessionRevocations"/> decorator that memoises each user's "sessions valid after" (E10.5), which
/// the session validator asks for on every authenticated request: a dictionary hit once
/// <see cref="SessionRevocationsCache"/> holds it, else the store answers and the cache keeps the answer. A
/// revocation passes through and invalidates; revocations and disablements on other instances arrive through
/// the row-version probe.
/// </summary>
public sealed class CachedSessionRevocations : ISessionRevocations
{
    private readonly ISessionRevocations _inner;
    private readonly SessionRevocationsCache _cache;



    /// <summary>
    /// Creates the decorator over <paramref name="inner"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public CachedSessionRevocations(ISessionRevocations inner, SessionRevocationsCache cache)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }



    /// <inheritdoc/>
    public async Task<DateTimeOffset?> SessionsValidAfterAsync(long userId, CancellationToken cancellationToken)
    {
        var answers = await _cache.GetAsync(cancellationToken).ConfigureAwait(false);
        if (answers.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var answer = await _inner.SessionsValidAfterAsync(userId, cancellationToken).ConfigureAwait(false);
        answers[userId] = answer;
        return answer;
    }



    /// <inheritdoc/>
    public async Task RevokeSessionsAsync(long userId, CancellationToken cancellationToken)
    {
        await _inner.RevokeSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
        _cache.Invalidate();
    }
}
