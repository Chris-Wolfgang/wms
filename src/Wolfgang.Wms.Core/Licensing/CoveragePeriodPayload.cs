// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// One coverage period in a key document (E79.5): <c>{"from":"2026-01-01","to":"2026-12-31"}</c>.
/// </summary>
internal sealed record CoveragePeriodPayload
{
    /// <summary>The first covered day.</summary>
    public DateOnly From { get; init; }

    /// <summary>The last covered day.</summary>
    public DateOnly To { get; init; }
}
