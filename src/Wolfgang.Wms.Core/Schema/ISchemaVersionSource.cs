// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Schema;

/// <summary>
/// Reports the database schema status (E82.5). Infrastructure implements it over the EF migrations history
/// (E2); until then <see cref="NotInstalledSchemaVersionSource"/> reports no database.
/// </summary>
public interface ISchemaVersionSource
{
    /// <summary>
    /// The current and expected migration identifiers.
    /// </summary>
    Task<SchemaStatus> GetAsync(CancellationToken cancellationToken);
}
