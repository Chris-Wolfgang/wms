// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// What happened to one row (E16.6), or what would have happened under <see cref="ImportPolicy.ValidateOnly"/>
/// and for the valid rows of a rolled-back <see cref="ImportPolicy.AllOrNothing"/> run.
/// </summary>
public enum ImportRowOutcome
{
    /// <summary>The key was new; a row was (or would be) created.</summary>
    Inserted,

    /// <summary>The key existed and a field differed; the row was (or would be) replaced.</summary>
    Updated,

    /// <summary>The key existed and was active; the row was (or would be) retired.</summary>
    Deleted,

    /// <summary>The key existed and nothing differed (or it was already retired); nothing to do.</summary>
    Unchanged,

    /// <summary>The row was refused; <see cref="ImportRowResult.Code"/> and <see cref="ImportRowResult.Message"/> say why.</summary>
    Failed,
}
