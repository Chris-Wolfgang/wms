// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// The database engines an installation can run on (E2). Chosen at install time through
/// <c>Wms:Database:Provider</c>; the model and the migrations exist for both.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// No database: the host starts for bootstrap only (schema endpoint, health), nothing else works.
    /// </summary>
    None = 0,

    /// <summary>
    /// Microsoft SQL Server 2022 or later, including Express.
    /// </summary>
    SqlServer = 1,

    /// <summary>
    /// PostgreSQL 16 or later.
    /// </summary>
    PostgreSql = 2,
}
