// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Schema;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// Schema status from the EF migrations history (E82.5, E2): <c>Current</c> is the last applied migration,
/// <c>Expected</c> the last one this build ships. When the database cannot be reached (not provisioned yet,
/// wrong credentials, server down) <c>Current</c> is null rather than an error, so the endpoint stays a
/// bootstrap signal.
/// </summary>
public sealed class MigrationsSchemaVersionSource : ISchemaVersionSource
{
    private readonly WmsDbContext _context;



    /// <summary>
    /// Creates the source over the request's context.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public MigrationsSchemaVersionSource(WmsDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }



    /// <inheritdoc/>
    public async Task<SchemaStatus> GetAsync(CancellationToken cancellationToken)
    {
        var expected = _context.Database.GetMigrations().LastOrDefault();
        string? current;
        try
        {
            current = (await _context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).LastOrDefault();
        }
        catch (DbException)
        {
            current = null;
        }

        return new SchemaStatus(current, expected);
    }
}
