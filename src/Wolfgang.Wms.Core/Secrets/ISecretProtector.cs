// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Secrets;

/// <summary>
/// Encrypts and decrypts the secrets the product stores (E8.5): the connection string in configuration
/// (E8.2) and secret-kind settings in the database (E8.3). The default implementation uses ASP.NET Core
/// Data Protection with the key ring configured by <c>Wms:DataProtection</c> (E8.1, E8.6); a customer with
/// a corporate vault registers their own and every secret flows through it instead. Implementations never
/// log the plain text.
/// </summary>
public interface ISecretProtector
{
    /// <summary>
    /// Encrypts <paramref name="plainText"/> into the <c>enc:v1:</c> form (<see cref="ProtectedText"/>).
    /// </summary>
    string Protect(string plainText);



    /// <summary>
    /// Decrypts a value produced by <see cref="Protect"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value is not in the protected form, or the key that encrypted it is not available.</exception>
    string Unprotect(string protectedText);
}
