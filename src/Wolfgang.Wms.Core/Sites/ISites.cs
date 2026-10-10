// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Sites;

/// <summary>
/// The sites store (E16.1): every warehouse the install runs, created and edited by administrators.
/// <c>AddWmsDatabase</c> replaces the placeholder with the stored one.
/// </summary>
public interface ISites
{
    /// <summary>
    /// Every site, active and retired, in code order.
    /// </summary>
    Task<IReadOnlyList<SiteInfo>> ListAsync(CancellationToken cancellationToken);



    /// <summary>
    /// One site by id.
    /// </summary>
    /// <exception cref="SiteException"><see cref="SiteErrorCodes.NotFound"/> when no site has that id.</exception>
    Task<SiteInfo> FindAsync(long siteId, CancellationToken cancellationToken);



    /// <summary>
    /// Creates a site from a valid draft.
    /// </summary>
    /// <exception cref="SiteException"><see cref="SiteErrorCodes.Invalid"/> or <see cref="SiteErrorCodes.CodeTaken"/>.</exception>
    Task<SiteInfo> CreateAsync(SiteDraft draft, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Replaces a site's details. Deactivating a site (<see cref="SiteDraft.IsActive"/> false on an active site)
    /// is refused while <see cref="IOpenReleases"/> reports open releases for it.
    /// </summary>
    /// <exception cref="SiteException"><see cref="SiteErrorCodes.NotFound"/>, <see cref="SiteErrorCodes.Invalid"/>, <see cref="SiteErrorCodes.CodeTaken"/> or <see cref="SiteErrorCodes.HasOpenReleases"/>.</exception>
    Task<SiteInfo> UpdateAsync(long siteId, SiteDraft draft, string updatedBy, CancellationToken cancellationToken);
}
