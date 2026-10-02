// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Core.Http;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// Turns a <see cref="SettingException"/> into the problem response its code describes (E6.3), so the
/// endpoints and the console share one error path with the accessor.
/// </summary>
public sealed class SettingExceptionHandler : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not SettingException failure)
        {
            return false;
        }

        await ApiProblems.Problem(failure.Code, failure.Message).ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }
}
