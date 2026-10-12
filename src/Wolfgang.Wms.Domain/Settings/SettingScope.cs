// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Settings;

/// <summary>
/// Where a setting value is held (E6.1, E7). Values cascade down two chains: organisation → site → zone for
/// operational settings, and organisation → site → SKU for product policies (E7.4); a child without its own
/// configured value takes its parent's effective value.
/// </summary>
public enum SettingScope
{
    /// <summary>The whole installation; there is exactly one organisation.</summary>
    Organization = 1,

    /// <summary>One warehouse.</summary>
    Site = 2,

    /// <summary>One zone of a site.</summary>
    Zone = 3,

    /// <summary>One product, for lot, serial and similar policies.</summary>
    Sku = 4,
}
