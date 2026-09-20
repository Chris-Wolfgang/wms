// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Identifiers;

/// <summary>
/// How a validation profile normalises letter case before checking a customer-supplied identifier (E3.7).
/// </summary>
public enum CaseHandling
{
    /// <summary>
    /// Store exactly as received (the default).
    /// </summary>
    NoChange = 0,

    /// <summary>
    /// Upper-case the value (invariant culture).
    /// </summary>
    MakeUpper = 1,

    /// <summary>
    /// Lower-case the value (invariant culture).
    /// </summary>
    MakeLower = 2,
}
