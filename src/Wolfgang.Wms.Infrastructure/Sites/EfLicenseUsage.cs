// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Licensing;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Licensing;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Sites;

/// <summary>
/// <see cref="ILicenseUsage"/> over the stored rows (E16.1): active sites count against the site limit. Limits
/// whose rows do not exist yet report zero until their stories arrive.
/// </summary>
public sealed class EfLicenseUsage : ILicenseUsage
{
    private readonly WmsDbContext _context;



    /// <summary>
    /// Creates the counter.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public EfLicenseUsage(WmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }



    /// <inheritdoc/>
    public async Task<int> CountAsync(LicenseLimit limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(limit);

        return string.Equals(limit.Name, LicenseLimits.Sites.Name, StringComparison.Ordinal)
            ? await _context.Sites.CountAsync(s => s.IsActive, cancellationToken).ConfigureAwait(false)
            : 0;
    }
}
