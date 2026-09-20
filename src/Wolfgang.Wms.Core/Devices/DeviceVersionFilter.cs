// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolfgang.Wms.Core.Http;

namespace Wolfgang.Wms.Core.Devices;

/// <summary>
/// Endpoint filter for device endpoints (E82.7): the request must carry a parseable
/// <see cref="DeviceVersion.HeaderName"/>; when the site has a minimum and the app is older, the answer is
/// <c>426 Upgrade Required</c> with the minimum in the problem's <c>minimumVersion</c> extension so the
/// device can offer the update (E37.5).
/// </summary>
public sealed class DeviceVersionFilter : IEndpointFilter
{
    /// <summary>
    /// Problem-details extension carrying the minimum version on a 426.
    /// </summary>
    public const string MinimumVersionExtension = "minimumVersion";

    private readonly IDeviceVersionPolicy _policy;



    /// <summary>
    /// Creates the filter over the site's minimum-version policy.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    public DeviceVersionFilter(IDeviceVersionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        _policy = policy;
    }



    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var header = context.HttpContext.Request.Headers[DeviceVersion.HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return ApiProblems.Problem(DeviceErrorCodes.VersionMissing);
        }

        if (!DeviceVersion.TryParse(header, out var version))
        {
            return ApiProblems.Problem(DeviceErrorCodes.VersionInvalid, arguments: header);
        }

        var minimum = await _policy.GetMinimumAsync(context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (minimum is not null && !DeviceVersion.Satisfies(version, minimum))
        {
            var problem = (ProblemHttpResult)ApiProblems.Problem(DeviceErrorCodes.VersionTooOld, arguments: [version.ToString(), minimum.ToString()]);
            problem.ProblemDetails.Extensions[MinimumVersionExtension] = minimum.ToString();
            return problem;
        }

        return await next(context).ConfigureAwait(false);
    }
}
