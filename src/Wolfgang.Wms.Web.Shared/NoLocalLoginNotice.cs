// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Web.Shared;

/// <summary>
/// Placeholder notice source until the console talks to the API (E82.8 wiring): never a window, so never a
/// banner. Replaced by <see cref="ApiLocalLoginNotice"/> without touching any component.
/// </summary>
public sealed class NoLocalLoginNotice : ILocalLoginNotice
{
    /// <inheritdoc/>
    public Task<LocalLoginNotice?> GetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<LocalLoginNotice?>(null);
    }
}
