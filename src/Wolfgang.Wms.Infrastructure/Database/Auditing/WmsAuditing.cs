// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Wolfgang.AuditTrail;

namespace Wolfgang.Wms.Infrastructure.Database.Auditing;

/// <summary>
/// The one audit store (E6.4): AuditTrail (Model 1, <see cref="AuditingDbContext"/>) records every insert,
/// update and delete of every audited entity into <c>core.audit_header</c> (who, when, entity, key,
/// operation, transaction) and <c>core.audit_detail</c> (one row per changed column) in the same transaction
/// as the change. Every table is audited unless it carries <see cref="NotAuditedAttribute"/>; high-volume
/// picking tables opt out. Settings, master data, roles, leases, API keys and validation profiles share this
/// store, one query API and one console viewer.
/// </summary>
public static class WmsAuditing
{
    /// <summary>
    /// The schema the audit tables live in.
    /// </summary>
    public const string Schema = "core";



    /// <summary>
    /// The header table.
    /// </summary>
    public const string HeaderTable = "audit_header";



    /// <summary>
    /// The detail table.
    /// </summary>
    public const string DetailTable = "audit_detail";



    /// <summary>
    /// The identity recorded as <c>user_id</c> when no host is running (design time, the migrate tool,
    /// tests); a host records its application name.
    /// </summary>
    public const string SystemIdentity = "system";



    /// <summary>
    /// PostgreSQL batch cap: AuditTrail's benchmarks show the per-save overhead stops paying off above it.
    /// </summary>
    public const int PostgreSqlMaxBatchSize = 100;



    /// <summary>
    /// The options every context instance uses: the <c>core</c> tables and captured values on delete (soft
    /// deletes are updates; a hard delete keeps its last state for forensics).
    /// </summary>
    public static AuditOptions Options()
    {
        return new AuditOptions
        {
            Schema = Schema,
            HeaderTableName = HeaderTable,
            DetailTableName = DetailTable,
            CaptureDeletedValues = true,
        };
    }



    /// <summary>
    /// Registers AuditTrail with <see cref="HttpAuditUserProvider"/>: the host's application name as the
    /// service identity and the signed-in user, when there is one, on behalf of whom it acted.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsAuditing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddEfCoreAuditing<HttpAuditUserProvider>(options =>
        {
            options.Schema = Schema;
            options.HeaderTableName = HeaderTable;
            options.DetailTableName = DetailTable;
            options.CaptureDeletedValues = true;
        });
        return services;
    }
}
