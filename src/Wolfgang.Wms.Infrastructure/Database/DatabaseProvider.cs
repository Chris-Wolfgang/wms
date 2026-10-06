// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// The database engines an installation can run on (E2). Chosen at install time through
/// <c>Wms:Database:Provider</c>; the model and the migrations exist for both.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// No database configured (the shipped default): the host starts so an installation can be checked before
    /// a database is set up; <c>GET /api/v0/system/schema</c> answers and reports no current version, and nothing
    /// that needs data works.
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
