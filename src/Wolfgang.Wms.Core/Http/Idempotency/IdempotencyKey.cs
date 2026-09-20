// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Http.Idempotency;

/// <summary>
/// The value of an <c>Idempotency-Key</c> request header (E82.3): a client-chosen token, 1 to 128 visible
/// ASCII characters, unique per caller for 24 hours. The console generates one when a form opens; devices
/// generate one per action so a retried request can never create a second side effect.
/// </summary>
public readonly record struct IdempotencyKey
{
    /// <summary>
    /// The request header carrying the key.
    /// </summary>
    public const string HeaderName = "Idempotency-Key";



    /// <summary>
    /// Longest accepted key.
    /// </summary>
    public const int MaxLength = 128;



    private IdempotencyKey(string value)
    {
        Value = value;
    }



    /// <summary>
    /// The key text exactly as sent.
    /// </summary>
    public string Value { get; }



    /// <summary>
    /// Parses a header value; false when it is missing, blank, too long, or contains anything other than
    /// visible ASCII (0x21..0x7E).
    /// </summary>
    public static bool TryParse(string? headerValue, out IdempotencyKey key)
    {
        key = default;
        if (string.IsNullOrEmpty(headerValue) || headerValue.Length > MaxLength)
        {
            return false;
        }

        foreach (var c in headerValue)
        {
            if (c is < '!' or > '~')
            {
                return false;
            }
        }

        key = new IdempotencyKey(headerValue);
        return true;
    }



    /// <inheritdoc/>
    public override string ToString()
    {
        return Value ?? string.Empty;
    }
}
