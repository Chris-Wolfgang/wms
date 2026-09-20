// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Security.Cryptography;

namespace Wolfgang.Wms.Core.Http.Idempotency;

/// <summary>
/// The idempotency rules (E82.3) as pure functions, so the endpoint filter and the store implementation share
/// one definition: bodies are compared by fingerprint, records live 24 hours, and the decision for a repeat is
/// replay, conflict or proceed.
/// </summary>
public static class Idempotency
{
    /// <summary>
    /// How long a record is honoured after it is stored.
    /// </summary>
    public static TimeSpan Retention { get; } = TimeSpan.FromHours(24);



    /// <summary>
    /// The fingerprint of a request body: lower-case hex SHA-256. An empty body has a fingerprint too.
    /// </summary>
    public static string Fingerprint(ReadOnlySpan<byte> body)
    {
        return Convert.ToHexStringLower(SHA256.HashData(body));
    }



    /// <summary>
    /// What to do with a request whose key was looked up: proceed when nothing is stored or the record has
    /// expired, replay when the stored fingerprint matches, conflict when it does not.
    /// </summary>
    /// <param name="existing">The stored record for this caller and key, or null.</param>
    /// <param name="requestFingerprint">Fingerprint of the incoming body.</param>
    /// <param name="now">The current time (UTC), from the injected <see cref="TimeProvider"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="requestFingerprint"/> is blank.</exception>
    public static IdempotencyDecision Decide(IdempotencyRecord? existing, string requestFingerprint, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(requestFingerprint))
        {
            throw new ArgumentException("A fingerprint is required.", nameof(requestFingerprint));
        }

        if (existing is null || existing.IsExpired(now))
        {
            return IdempotencyDecision.Proceed;
        }

        return string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal)
            ? IdempotencyDecision.Replay
            : IdempotencyDecision.Conflict;
    }
}
