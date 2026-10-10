// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The usage counter before the counted tables exist (E79.8): every limit is at zero. The sites and devices
/// stories replace it with a counter over their tables; <c>max_totes_per_picker</c> and <c>users</c> are
/// caps, not counts, and stay at zero here.
/// </summary>
public sealed class NoLicenseUsage : ILicenseUsage
{
    /// <inheritdoc/>
    public Task<int> CountAsync(LicenseLimit limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(limit);
        return Task.FromResult(0);
    }
}
