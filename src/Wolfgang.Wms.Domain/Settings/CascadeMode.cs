// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Settings;

/// <summary>
/// How a scope takes part in the cascade (E7.2): it holds a value (or inherits one), or it delegates the
/// decision to a scope below it. An organisation may say "per site" or "per zone", a site "per zone"; a
/// zone or a SKU always holds a value. A delegating scope contributes no value of its own, and scopes
/// between it and the deciding level cannot configure the setting.
/// </summary>
public enum CascadeMode
{
    /// <summary>The scope holds a value, or inherits one.</summary>
    Value = 0,

    /// <summary>Each site decides.</summary>
    PerSite = 1,

    /// <summary>Each zone decides.</summary>
    PerZone = 2,

    /// <summary>Each SKU decides.</summary>
    PerSku = 3,
}
