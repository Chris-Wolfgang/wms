// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Sites;

/// <summary>
/// What keeps a site from being deactivated (E16.1): the releases still open against it. Release intake (E22)
/// supplies the stored answer; until then <see cref="NoOpenReleases"/> reports none, so deactivation is
/// never blocked.
/// </summary>
public interface IOpenReleases
{
    /// <summary>
    /// How many releases are open (received and not yet completed or cancelled) at a site.
    /// </summary>
    Task<int> CountOpenAsync(long siteId, CancellationToken cancellationToken);
}
