// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// The outcome of <see cref="MigrationRunner.Script"/> (E4.2, E4.6): the SQL, or no SQL when the script is a
/// downgrade that loses data and <c>--confirm-data-loss</c> was absent.
/// </summary>
/// <param name="Direction">Up, Down, or None when <c>--from</c> and <c>--to</c> name the same migration.</param>
/// <param name="Sql">The idempotent script, or null when the downgrade needs confirmation.</param>
/// <param name="DestructiveSteps">The data-losing Down operations the script runs (listed in its header).</param>
public sealed record MigrationScript(MigrationDirection Direction, string? Sql, IReadOnlyList<string> DestructiveSteps)
{
    /// <summary>
    /// True when no SQL was produced because the downgrade loses data and was not confirmed.
    /// </summary>
    public bool RequiresConfirmation => Sql is null;
}
