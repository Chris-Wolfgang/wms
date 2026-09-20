// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// Signs a key into the document <see cref="LicenseVerifier"/> reads (E79.3). The product ships this for
/// the vendor's issuing tool and for tests with their own key pair; the vendor's private key is never in
/// the product or the repository.
/// </summary>
public static class LicenseKeySigner
{
    /// <summary>
    /// The signed document for <paramref name="key"/>, one line of compact JSON.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static string Sign(LicenseKey key, ECDsa privateKey)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(privateKey);

        var payload = JsonSerializer.SerializeToUtf8Bytes(LicenseKeyJson.ToPayload(key), LicenseJsonContext.Default.LicenseKeyPayload);
        var signature = privateKey.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var envelope = new LicenseKeyEnvelope
        {
            Payload = Base64Url.EncodeToString(payload),
            Signature = Base64Url.EncodeToString(signature),
            Algorithm = LicenseKeyEnvelope.Es256,
        };
        return JsonSerializer.Serialize(envelope, LicenseJsonContext.Default.LicenseKeyEnvelope);
    }
}
