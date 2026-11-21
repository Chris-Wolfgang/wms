// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The current count of what a limit counts (E79.4, E79.8): recomputed from the data at check time, never
/// cached or incremented, so a row inserted outside the application cannot quietly exceed a tier. Sites are
/// counted as they exist; devices as those active in the trailing window (heartbeat or login seen), never
/// enrolled units, and never console users.
/// </summary>
public interface ILicenseUsage
{
    /// <summary>
    /// The count for <paramref name="limit"/> now.
    /// </summary>
    Task<int> CountAsync(LicenseLimit limit, CancellationToken cancellationToken);
}
