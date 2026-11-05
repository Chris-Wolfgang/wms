// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Core.Http;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// Turns a <see cref="LicenseException"/> into the problem response its code describes (E79.4), so a
/// creation any module refuses through the gate answers the same way.
/// </summary>
public sealed class LicenseExceptionHandler : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not LicenseException failure)
        {
            return false;
        }

        await ApiProblems.Problem(failure.Code, failure.Message, failure.Message).ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }
}
