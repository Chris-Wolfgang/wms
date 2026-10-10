// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// The master data importer (E16.6): idempotent upserts by natural key with a bad-row policy per run.
/// <c>AddWmsDatabase</c> replaces the placeholder with the stored one.
/// </summary>
public interface IImports
{
    /// <summary>
    /// Loads a zones file into a site.
    /// </summary>
    /// <exception cref="ImportException"><see cref="ImportErrorCodes.SiteNotFound"/> or <see cref="ImportErrorCodes.Invalid"/>.</exception>
    Task<ImportResult> ImportZonesAsync(long siteId, IReadOnlyList<ZoneImportRow> rows, ImportPolicy policy, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Loads a locations file into a site; the zones the rows name must already exist.
    /// </summary>
    /// <exception cref="ImportException"><see cref="ImportErrorCodes.SiteNotFound"/> or <see cref="ImportErrorCodes.Invalid"/>.</exception>
    Task<ImportResult> ImportLocationsAsync(long siteId, IReadOnlyList<LocationImportRow> rows, ImportPolicy policy, string updatedBy, CancellationToken cancellationToken);
}
