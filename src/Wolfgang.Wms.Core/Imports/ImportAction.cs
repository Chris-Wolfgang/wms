// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// What a row asks for (E16.6): an upsert by natural key (the default) or a soft delete.
/// </summary>
public enum ImportAction
{
    /// <summary>Insert the row when its key is new, update it when the key exists; re-running a file is safe.</summary>
    Upsert,

    /// <summary>Retire the row with that key (<c>isActive: false</c>); rows are never removed.</summary>
    Delete,
}
