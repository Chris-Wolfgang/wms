// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Settings;

/// <summary>
/// The value type of a secret setting (E6.1, E8.3): a credential the store encrypts at rest and the console
/// masks. It never appears in <see cref="ToString"/> or logs; read <see cref="Value"/> deliberately.
/// </summary>
public readonly record struct SecretText
{
    /// <summary>
    /// Wraps a secret; an empty string is a valid "not set".
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public SecretText(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }



    /// <summary>
    /// The plain secret.
    /// </summary>
    public string Value { get; }



    /// <summary>
    /// True when no secret is set.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);



    /// <summary>
    /// A mask, never the secret.
    /// </summary>
    public override string ToString()
    {
        return IsEmpty ? string.Empty : "••••••";
    }
}
