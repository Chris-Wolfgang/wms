// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Imports;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Core.Zones;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Imports;

/// <summary>
/// <see cref="IImports"/> over the stored master data (E16.6): checks the site and the file, then hands the
/// rows to the entity's importer, which plans every row against the site's current rows, decides under the
/// policy whether to write, and writes the planned rows in one transaction.
/// </summary>
public sealed class EfImports : IImports
{
    private readonly WmsDbContext _context;
    private readonly ZoneImporter _zones;
    private readonly LocationImporter _locations;



    /// <summary>
    /// Creates the importer.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfImports(WmsDbContext context, TimeProvider timeProvider, ISettings settings, IOpenZoneGroups openGroups)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(openGroups);
        _zones = new ZoneImporter(context, timeProvider, settings, openGroups);
        _locations = new LocationImporter(context, timeProvider);
    }



    /// <inheritdoc/>
    public async Task<ImportResult> ImportZonesAsync(long siteId, IReadOnlyList<ZoneImportRow> rows, ImportPolicy policy, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        await RequireFileAsync(siteId, rows.Count, cancellationToken).ConfigureAwait(false);
        return await _zones.RunAsync(siteId, rows, policy, updatedBy, cancellationToken).ConfigureAwait(false);
    }



    /// <inheritdoc/>
    public async Task<ImportResult> ImportLocationsAsync(long siteId, IReadOnlyList<LocationImportRow> rows, ImportPolicy policy, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        await RequireFileAsync(siteId, rows.Count, cancellationToken).ConfigureAwait(false);
        return await _locations.RunAsync(siteId, rows, policy, updatedBy, cancellationToken).ConfigureAwait(false);
    }



    private async Task RequireFileAsync(long siteId, int rowCount, CancellationToken cancellationToken)
    {
        if (ImportRules.ValidateSize(rowCount) is { } reason)
        {
            throw new ImportException(ImportErrorCodes.Invalid, reason);
        }

        if (!await _context.Sites.AnyAsync(s => s.Id == siteId, cancellationToken).ConfigureAwait(false))
        {
            throw new ImportException(ImportErrorCodes.SiteNotFound, $"Site {siteId} does not exist.");
        }
    }
}
