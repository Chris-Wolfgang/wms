// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// Which way a migration run moved the schema. The command surface is declarative (a target, never an
/// <c>--up</c>/<c>--down</c> flag); the tool states the direction it took (E4.6).
/// </summary>
public enum MigrationDirection
{
    /// <summary>
    /// The database was already at the target.
    /// </summary>
    None = 0,

    /// <summary>
    /// Migrations were applied.
    /// </summary>
    Up = 1,

    /// <summary>
    /// Migrations were reverted.
    /// </summary>
    Down = 2,
}
