// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;

namespace Wolfgang.Wms.Core.Http;

/// <summary>
/// HTTP validation for API reads (E1.12, ADR 0003): every response carries the resource's
/// <see cref="EntityTag"/> and <see cref="CacheControl.Api"/>, and a request whose <c>If-None-Match</c>
/// names the current tag gets <c>304 Not Modified</c> without the body being produced.
/// </summary>
public static class ConditionalResults
{
    /// <summary>
    /// Sets <c>ETag</c> and <c>Cache-Control</c> on the response, then returns <c>304</c> when the request's
    /// <c>If-None-Match</c> matches <paramref name="tag"/>, otherwise the result of <paramref name="produce"/>.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="tag">The current tag of the resource, read from its version column.</param>
    /// <param name="produce">Builds the full response; called only when the client's copy is stale.</param>
    public static IResult NotModifiedOr(HttpRequest request, EntityTag tag, Func<IResult> produce)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(produce);

        var responseHeaders = request.HttpContext.Response.Headers;
        responseHeaders.ETag = tag.Value;
        responseHeaders.CacheControl = CacheControl.Api;

        return tag.IsMatchedBy(request.Headers.IfNoneMatch)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : produce();
    }
}
