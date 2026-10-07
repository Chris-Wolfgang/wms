// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Core.Http;

namespace Wolfgang.Wms.Core.Sites;

/// <summary>
/// Turns a <see cref="SiteException"/> into the problem response its code describes (E16.1).
/// </summary>
public sealed class SiteExceptionHandler : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not SiteException failure)
        {
            return false;
        }

        await ApiProblems.Problem(failure.Code, failure.Message, failure.Message).ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }
}
