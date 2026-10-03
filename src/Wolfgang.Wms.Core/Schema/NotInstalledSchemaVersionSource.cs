// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Schema;

/// <summary>
/// Reports that no database is configured: both identifiers null, never up to date. The placeholder until
/// the EF-backed source arrives with the data model (E2).
/// </summary>
public sealed class NotInstalledSchemaVersionSource : ISchemaVersionSource
{
    /// <inheritdoc/>
    public Task<SchemaStatus> GetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new SchemaStatus(Current: null, Expected: null));
    }
}
