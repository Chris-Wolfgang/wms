// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Authorization;

namespace Wolfgang.Wms.Core.Sites;

/// <summary>
/// The sites store of a host without a database (E16.1): every call answers
/// <see cref="SiteErrorCodes.Unavailable"/>.
/// </summary>
public sealed class NoSites : ISites
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<SiteInfo>> ListAsync(SiteScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<SiteInfo> FindAsync(long siteId, CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<SiteInfo> CreateAsync(SiteDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<SiteInfo> UpdateAsync(long siteId, SiteDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    private static SiteException Unavailable()
    {
        return new SiteException(SiteErrorCodes.Unavailable, "The database is not configured; there are no sites to read or write.");
    }
}
