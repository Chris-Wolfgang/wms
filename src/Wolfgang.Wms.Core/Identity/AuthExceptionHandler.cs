// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Core.Http;

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// Turns an <see cref="AuthException"/> into the problem response its code describes (E9).
/// </summary>
public sealed class AuthExceptionHandler : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not AuthException failure)
        {
            return false;
        }

        await ApiProblems.Problem(failure.Code, failure.Message).ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }
}
