// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// The grants before a database exists (bootstrap): a session keeps the grants it was issued with.
/// </summary>
public sealed class NoSessionGrants : ISessionGrants
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<string>?> GrantsOfAsync(long userId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<string>?>(null);
    }
}
