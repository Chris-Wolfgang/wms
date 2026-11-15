// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Core.Http;

namespace Wolfgang.Wms.Core.Organization;

/// <summary>
/// Turns an <see cref="OrganizationException"/> into the problem its code names (E16.0, E82.3).
/// </summary>
public sealed class OrganizationExceptionHandler : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not OrganizationException failure)
        {
            return false;
        }

        await ApiProblems.Problem(failure.Code, failure.Message, failure.Message).ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }
}
