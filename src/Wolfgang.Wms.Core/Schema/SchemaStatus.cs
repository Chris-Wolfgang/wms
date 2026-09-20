// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Schema;

/// <summary>
/// The database schema as the API sees it (E82.5): the migration the database is at and the one this build
/// expects. Read-only; applying migrations is a bootstrap step outside the API (<c>wms migrate</c>).
/// </summary>
/// <param name="Current">Identifier of the last applied migration, or null when no database is reachable or none was applied.</param>
/// <param name="Expected">Identifier of the last migration this build ships, or null before the data model exists.</param>
public sealed record SchemaStatus(string? Current, string? Expected)
{
    /// <summary>
    /// True when the database is at exactly the migration this build expects.
    /// </summary>
    public bool UpToDate => Current is not null && string.Equals(Current, Expected, StringComparison.Ordinal);
}
