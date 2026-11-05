// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// What an import does with bad rows (E16.6), chosen per run with a default per entity type.
/// </summary>
public enum ImportPolicy
{
    /// <summary>Rolls the whole file back when any row fails: the default for structural data (zones, paths), where a partial load leaves the site inconsistent.</summary>
    AllOrNothing,

    /// <summary>Loads the good rows and reports the bad: the default for independent rows (locations, SKUs, barcodes).</summary>
    AcceptValidRows,

    /// <summary>Runs the full validation and writes nothing, for checking a file before loading it.</summary>
    ValidateOnly,
}
