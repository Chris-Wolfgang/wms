// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Http;

/// <summary>
/// Error codes for optimistic concurrency (E5.2), defined once (E1.13).
/// </summary>
public static class ConcurrencyErrorCodes
{
    /// <summary>
    /// The <c>If-Match</c> tag the client sent is not the resource's current <c>row_version</c>, or the
    /// row changed between read and save: someone else changed it. Reload and retry.
    /// </summary>
    public static ErrorCode PreconditionFailed { get; } = new
    (
        "concurrency.precondition_failed",
        StatusCodes.Status412PreconditionFailed,
        "The record changed since you read it; reload and try again.",
        "concurrency-precondition-failed",
        ErrorSeverity.Warning
    );



    /// <summary>
    /// An update or delete sent no <c>If-Match</c>; conditional requests are required so a stale copy can
    /// never overwrite someone else's work.
    /// </summary>
    public static ErrorCode PreconditionRequired { get; } = new
    (
        "concurrency.precondition_required",
        StatusCodes.Status428PreconditionRequired,
        "Send If-Match with the ETag you read.",
        "concurrency-precondition-required",
        ErrorSeverity.Error
    );
}
