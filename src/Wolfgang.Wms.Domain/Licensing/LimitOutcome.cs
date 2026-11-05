// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// What a creation against a limit may do (E79.4).
/// </summary>
public enum LimitOutcome
{
    /// <summary>Under the limit: go ahead.</summary>
    Allowed,

    /// <summary>Over the limit but within the allowance and the grace period: go ahead, with a banner, an issue and reminders.</summary>
    WithinAllowance,

    /// <summary>Beyond the allowance, past the grace period, or the coverage lapsed: refused with the limit and tier named.</summary>
    Blocked,
}
