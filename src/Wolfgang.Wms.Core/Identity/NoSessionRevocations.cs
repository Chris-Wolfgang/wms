// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// The revocations before a database exists (bootstrap): nothing is revoked.
/// </summary>
public sealed class NoSessionRevocations : ISessionRevocations
{
    /// <inheritdoc/>
    public Task<DateTimeOffset?> SessionsValidAfterAsync(long userId, CancellationToken cancellationToken)
    {
        return Task.FromResult<DateTimeOffset?>(null);
    }



    /// <inheritdoc/>
    public Task RevokeSessionsAsync(long userId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
