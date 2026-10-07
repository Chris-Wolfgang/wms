// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Devices;

/// <summary>
/// Accepts every device version: the placeholder until the minimum-version setting exists (E12).
/// </summary>
public sealed class NoMinimumDeviceVersionPolicy : IDeviceVersionPolicy
{
    /// <inheritdoc/>
    public Task<Version?> GetMinimumAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<Version?>(null);
    }
}
