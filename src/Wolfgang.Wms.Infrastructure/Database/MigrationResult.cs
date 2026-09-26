// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// The outcome of <see cref="MigrationRunner.ApplyAsync"/> (E4.1, E4.6): the direction the tool took, the
/// migrations it ran, and either the failing migration or the destructive steps that need
/// <c>--confirm-data-loss</c>.
/// </summary>
/// <param name="Direction">Up, Down, or None when the database was already at the target.</param>
/// <param name="From">The migration the database was at before, or null for an empty database.</param>
/// <param name="To">The migration the database is at now (or would be at), or null for empty.</param>
/// <param name="Steps">Migrations applied or reverted, in the order they ran.</param>
/// <param name="FailedMigration">The migration that failed, or null.</param>
/// <param name="Error">The failure message, or null.</param>
/// <param name="DestructiveSteps">Down operations that lose data, listed when a downgrade needs confirmation.</param>
public sealed record MigrationResult
(
    MigrationDirection Direction,
    string? From,
    string? To,
    IReadOnlyList<string> Steps,
    string? FailedMigration,
    string? Error,
    IReadOnlyList<string> DestructiveSteps
)
{
    /// <summary>
    /// True when the downgrade was not run because it would lose data and <c>--confirm-data-loss</c> was absent.
    /// </summary>
    public bool RequiresConfirmation => DestructiveSteps.Count > 0 && FailedMigration is null && Steps.Count == 0 && Direction == MigrationDirection.Down;



    /// <summary>
    /// True when every step ran.
    /// </summary>
    public bool Succeeded => FailedMigration is null && !RequiresConfirmation;
}
