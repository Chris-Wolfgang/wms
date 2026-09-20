// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;

namespace Wolfgang.Wms.Core.Http;

/// <summary>
/// Conditional updates (E5.2, E82.3): a client that changes or deletes a resource sends <c>If-Match</c> with
/// the <c>ETag</c> it read (the <c>row_version</c>, E1.12). A missing header is <c>428</c>, a stale one
/// <c>412</c>; only a matching one lets the handler run.
/// </summary>
public static class Preconditions
{
    /// <summary>
    /// The problem to return when the request's <c>If-Match</c> is missing or does not name
    /// <paramref name="current"/>, or null when the precondition holds.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="current">The resource's current tag, from its <c>row_version</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static IResult? RequireIfMatch(HttpRequest request, EntityTag current)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ifMatch = request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return ApiProblems.Problem(ConcurrencyErrorCodes.PreconditionRequired);
        }

        return current.IsMatchedBy(ifMatch) ? null : ApiProblems.Problem(ConcurrencyErrorCodes.PreconditionFailed);
    }
}
