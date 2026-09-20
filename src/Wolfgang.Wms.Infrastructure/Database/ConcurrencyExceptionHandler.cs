// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Http;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// Turns a stale save (<see cref="DbUpdateConcurrencyException"/>: the <c>row_version</c> concurrency token
/// changed between read and write, on either provider) into the same <c>412</c> problem as a failed
/// <c>If-Match</c> (E5.2), so the console can show "changed by someone else" with a reload either way.
/// </summary>
public sealed class ConcurrencyExceptionHandler : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not DbUpdateConcurrencyException)
        {
            return false;
        }

        await ApiProblems.Problem(ConcurrencyErrorCodes.PreconditionFailed).ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }
}
