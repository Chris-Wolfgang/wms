// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// The importer of a host without a database (E16.6): every call answers <see cref="ImportErrorCodes.Unavailable"/>.
/// </summary>
public sealed class NoImports : IImports
{
    /// <inheritdoc/>
    public Task<ImportResult> ImportZonesAsync(long siteId, IReadOnlyList<ZoneImportRow> rows, ImportPolicy policy, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    /// <inheritdoc/>
    public Task<ImportResult> ImportLocationsAsync(long siteId, IReadOnlyList<LocationImportRow> rows, ImportPolicy policy, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    private static ImportException Unavailable()
    {
        return new ImportException(ImportErrorCodes.Unavailable, "The database is not configured; nothing can be imported.");
    }
}
