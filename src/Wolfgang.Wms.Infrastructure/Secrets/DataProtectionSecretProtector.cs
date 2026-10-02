// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Wolfgang.Wms.Core.Secrets;

namespace Wolfgang.Wms.Infrastructure.Secrets;

/// <summary>
/// The default <see cref="ISecretProtector"/> (E8.5): ASP.NET Core Data Protection under one purpose, keys
/// from the configured ring. A value the ring cannot decrypt (the ring is missing, empty, or not the one that
/// encrypted it) fails with a message that names the configuration key to fix.
/// </summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;



    /// <summary>
    /// Creates the protector.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector(KeyRing.Purpose);
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="plainText"/> is null.</exception>
    public string Protect(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        return ProtectedText.Wrap(_protector.Protect(plainText));
    }



    /// <inheritdoc/>
    public string Unprotect(string protectedText)
    {
        string cipherText;
        try
        {
            cipherText = ProtectedText.Unwrap(protectedText);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }

        try
        {
            return _protector.Unprotect(cipherText);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException($"The value '{ProtectedText.Mask(protectedText)}' cannot be decrypted: the key ring does not hold the key that encrypted it. Point {KeyRingOptions.PathKey} at the key ring that was used, or re-encrypt the value with this one.", exception);
        }
    }
}
