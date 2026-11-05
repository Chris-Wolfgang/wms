// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Sites;

/// <summary>
/// <see cref="IOpenReleases"/> before release intake exists (E22): no site has open releases.
/// </summary>
public sealed class NoOpenReleases : IOpenReleases
{
    /// <inheritdoc/>
    public Task<int> CountOpenAsync(long siteId, CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }
}
