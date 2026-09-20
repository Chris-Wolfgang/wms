// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// What a key is (E79.11).
/// </summary>
public enum LicenseKeyKind
{
    /// <summary>The one base key of an install: tier, coverage, organization binding.</summary>
    Base,

    /// <summary>An add-on stacked on the base: devices, or a feature, with its own coverage.</summary>
    AddOn,
}
