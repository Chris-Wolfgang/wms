// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Wms.Core.Secrets;

/// <summary>
/// The stored form of an encrypted secret (E8.2): <c>enc:v1:</c> followed by the ciphertext. A value without
/// the prefix is plain text and is accepted as is (development, first run), so the same configuration key or
/// column holds either.
/// </summary>
public static class ProtectedText
{
    /// <summary>
    /// The prefix that marks an encrypted value; <c>v1</c> is the Data Protection format.
    /// </summary>
    public const string Prefix = "enc:v1:";



    /// <summary>
    /// True when <paramref name="text"/> carries the prefix.
    /// </summary>
    public static bool IsProtected([NotNullWhen(true)] string? text)
    {
        return text is not null && text.StartsWith(Prefix, StringComparison.Ordinal);
    }



    /// <summary>
    /// The prefixed form of a ciphertext.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="cipherText"/> is null or empty.</exception>
    public static string Wrap(string cipherText)
    {
        ArgumentException.ThrowIfNullOrEmpty(cipherText);
        return Prefix + cipherText;
    }



    /// <summary>
    /// The ciphertext behind the prefix.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="text"/> does not carry the prefix.</exception>
    public static string Unwrap(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!IsProtected(text) || text.Length == Prefix.Length)
        {
            throw new ArgumentException($"'{Mask(text)}' is not a protected value ({Prefix}...).", nameof(text));
        }

        return text[Prefix.Length..];
    }



    /// <summary>
    /// The plain text of a value that may or may not be protected: plain values pass through, protected ones
    /// are decrypted by <paramref name="protector"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value is protected and no protector is available.</exception>
    public static string? Reveal(string? text, ISecretProtector? protector)
    {
        if (!IsProtected(text))
        {
            return text;
        }

        if (protector is null)
        {
            throw new InvalidOperationException("The value is encrypted (enc:v1:) but no key ring is configured; set Wms:DataProtection:KeyRingPath to the key ring that encrypted it.");
        }

        return protector.Unprotect(text);
    }



    /// <summary>
    /// A loggable form of a value: the prefix and the first few characters, never a whole secret.
    /// </summary>
    public static string Mask(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Length <= 12 ? text[..Math.Min(4, text.Length)] + "…" : text[..12] + "…";
    }
}
