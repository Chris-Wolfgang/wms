// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Schema;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// Schema status from the EF migrations history (E82.5, E2): <c>Current</c> is the last applied migration,
/// <c>Expected</c> the last one this build ships. When the database cannot be reached (not provisioned yet,
/// wrong credentials, server down) <c>Current</c> is null rather than an error, so the endpoint stays a
/// bootstrap signal; the reason goes to the log at Warning (the provider's message, which names no secret), so
/// the bootstrap problems the endpoint exists to surface can be diagnosed from the API log.
/// </summary>
public sealed partial class MigrationsSchemaVersionSource : ISchemaVersionSource
{
    private readonly WmsDbContext _context;
    private readonly ILogger<MigrationsSchemaVersionSource> _logger;



    /// <summary>
    /// Creates the source over the request's context.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public MigrationsSchemaVersionSource(WmsDbContext context, ILogger<MigrationsSchemaVersionSource> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
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
        catch (DbException exception)
        {
            LogHistoryUnreadable(_logger, exception.Message);
            current = null;
        }

        return new SchemaStatus(current, expected);
    }



    [LoggerMessage(Level = LogLevel.Warning, Message = "The migrations history could not be read, so the schema endpoint reports no current migration: {Reason}")]
    private static partial void LogHistoryUnreadable(ILogger logger, string reason);
}
