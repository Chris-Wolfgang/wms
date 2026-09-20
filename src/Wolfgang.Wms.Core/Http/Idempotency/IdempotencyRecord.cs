// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Http.Idempotency;

/// <summary>
/// What the API remembers about a completed idempotent request (E82.3), stored per caller and key for
/// <see cref="Idempotency.Retention"/>: the fingerprint of the body that was accepted and the response that
/// was sent, so a retry with the same body gets the same answer and a retry with a different body gets 422.
/// </summary>
/// <param name="Caller">The authenticated caller the key belongs to (user or device id); keys never cross callers.</param>
/// <param name="Key">The client-chosen key.</param>
/// <param name="RequestFingerprint">SHA-256 of the request body, hex (<see cref="Idempotency.Fingerprint"/>).</param>
/// <param name="StatusCode">The status code of the stored response.</param>
/// <param name="ContentType">The stored response's media type, or null when the response had no body.</param>
/// <param name="Body">The stored response body, or null when there was none.</param>
/// <param name="StoredAt">When the response was stored (UTC); the record expires <see cref="Idempotency.Retention"/> later.</param>
public sealed record IdempotencyRecord
(
    string Caller,
    IdempotencyKey Key,
    string RequestFingerprint,
    int StatusCode,
    string? ContentType,
    byte[]? Body,
    DateTimeOffset StoredAt
)
{
    /// <summary>
    /// The authenticated caller; required.
    /// </summary>
    public string Caller { get; } = string.IsNullOrWhiteSpace(Caller) ? throw new ArgumentException("A caller is required.", nameof(Caller)) : Caller;



    /// <summary>
    /// Fingerprint of the accepted body; required.
    /// </summary>
    public string RequestFingerprint { get; } = string.IsNullOrWhiteSpace(RequestFingerprint) ? throw new ArgumentException("A fingerprint is required.", nameof(RequestFingerprint)) : RequestFingerprint;



    /// <summary>
    /// True once the record is older than <see cref="Idempotency.Retention"/> at <paramref name="now"/>.
    /// </summary>
    public bool IsExpired(DateTimeOffset now)
    {
        return now - StoredAt >= Idempotency.Retention;
    }
}
