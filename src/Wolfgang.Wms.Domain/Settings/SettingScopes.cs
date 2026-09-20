// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Settings;

/// <summary>
/// The set of scopes a setting may be configured at (E6.1). A key's allowed scopes form a chain from the
/// organisation down; a write at any other scope is rejected by the accessor.
/// </summary>
[Flags]
public enum SettingScopes
{
    /// <summary>No scope; never valid for a key.</summary>
    None = 0,

    /// <summary>Configurable at the organisation.</summary>
    Organization = 1,

    /// <summary>Configurable per site.</summary>
    Site = 2,

    /// <summary>Configurable per zone.</summary>
    Zone = 4,

    /// <summary>Configurable per SKU.</summary>
    Sku = 8,

    /// <summary>The operational chain: organisation, site and zone (the default for a key).</summary>
    OrganizationToZone = Organization | Site | Zone,

    /// <summary>The product chain: organisation, site and SKU.</summary>
    OrganizationToSku = Organization | Site | Sku,

    /// <summary>Organisation and site only.</summary>
    OrganizationToSite = Organization | Site,
}
