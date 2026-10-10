// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;

namespace Wolfgang.Wms.Core.Http;

/// <summary>
/// Conditional updates (E5.2, E82.3): a client that changes or deletes a resource sends <c>If-Match</c> with
/// the <c>ETag</c> it read (the <c>row_version</c>, E1.12). A missing header is <c>428</c>, and so is the
/// <c>*</c> wildcard: it would match any current row and prove nothing about what the client read, which is
/// the whole point of the check. A stale or weak (<c>W/</c>) tag is <c>412</c> (RFC 9110 requires the strong
/// comparison for <c>If-Match</c>); only an exactly matching strong tag lets the handler run.
/// </summary>
public static class Preconditions
{
    /// <summary>
    /// The problem to return when the request's <c>If-Match</c> is missing or a wildcard (<c>428</c>), or does
    /// not strongly match <paramref name="current"/> (<c>412</c>); null when the precondition holds.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="current">The resource's current tag, from its <c>row_version</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static IResult? RequireIfMatch(HttpRequest request, EntityTag current)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ifMatch = request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(ifMatch) || string.Equals(ifMatch.Trim(), "*", StringComparison.Ordinal))
        {
            return ApiProblems.Problem(ConcurrencyErrorCodes.PreconditionRequired);
        }

        return current.IsStronglyMatchedBy(ifMatch) ? null : ApiProblems.Problem(ConcurrencyErrorCodes.PreconditionFailed);
    }
}
