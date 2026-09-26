// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Wolfgang.Wms.Infrastructure.Database.Health;

/// <summary>
/// The readiness check of the database (E12.1): unhealthy when it cannot be reached, is behind this build
/// (pending migrations) or ahead of it (unknown migrations); healthy with the current schema version.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    /// <summary>
    /// The check name.
    /// </summary>
    public const string Name = "database";



    private readonly IServiceScopeFactory _scopes;



    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="scopes"/> is null.</exception>
    public DatabaseHealthCheck(IServiceScopeFactory scopes)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
    }



    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var scope = _scopes.CreateScope();
        var status = await scope.ServiceProvider.GetRequiredService<MigrationRunner>().StatusAsync(cancellationToken).ConfigureAwait(false);
        if (!status.Reachable)
        {
            return HealthCheckResult.Unhealthy("The database cannot be reached.");
        }

        if (status.SchemaIsNewer)
        {
            return HealthCheckResult.Unhealthy($"The database schema is newer than this build (unknown migrations: {string.Join(", ", status.Unknown)}).");
        }

        if (status.Pending.Count > 0)
        {
            return HealthCheckResult.Unhealthy($"The database schema is behind this build; pending migrations: {string.Join(", ", status.Pending)}.");
        }

        return HealthCheckResult.Healthy($"Schema {status.Current}.");
    }
}
