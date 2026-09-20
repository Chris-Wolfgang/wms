// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// How an installed key takes part in the effective license (E79.11).
/// </summary>
public enum KeyStatus
{
    /// <summary>Counted.</summary>
    Active,

    /// <summary>Its coverage does not contain this release's date (a base), or its base lapsed (an add-on); frozen, not counted for increases.</summary>
    Lapsed,

    /// <summary>Replaced by a later key that names it; ignored.</summary>
    Superseded,

    /// <summary>Bound to another organization than the base; ignored and flagged.</summary>
    ForeignOrganization,

    /// <summary>A second base key; only one base counts (the newest issued); ignored and flagged.</summary>
    ExtraBase,

    /// <summary>Its signature or format failed; ignored and flagged (set by the verifier).</summary>
    Invalid,
}
