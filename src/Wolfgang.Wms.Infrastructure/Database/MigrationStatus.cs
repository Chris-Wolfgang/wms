// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// What <c>wms-migrate --status</c> and the startup check see (E4.3, E4.4): the migrations this build ships,
/// which of them the database has, and whether the database is ahead of the build.
/// </summary>
/// <param name="Reachable">False when the database could not be queried; the other lists are then empty.</param>
/// <param name="Applied">Applied migrations in order, including any this build does not know.</param>
/// <param name="Pending">Migrations this build ships that the database lacks, in order.</param>
/// <param name="Expected">The last migration this build ships, or null when it ships none.</param>
public sealed record MigrationStatus(bool Reachable, IReadOnlyList<string> Applied, IReadOnlyList<string> Pending, string? Expected)
{
    /// <summary>
    /// Migrations the database has that this build does not know: the schema is newer than the app.
    /// </summary>
    public IReadOnlyList<string> Unknown { get; init; } = [];



    /// <summary>
    /// True when the database is reachable, has every shipped migration and nothing the build lacks.
    /// </summary>
    public bool UpToDate => Reachable && Pending.Count == 0 && Unknown.Count == 0;



    /// <summary>
    /// True when the database carries migrations this build does not ship (E4.6: the app refuses to start).
    /// </summary>
    public bool SchemaIsNewer => Unknown.Count > 0;



    /// <summary>
    /// The last applied migration, or null.
    /// </summary>
    public string? Current => Applied.Count == 0 ? null : Applied[^1];
}
