// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// Verifies pasted key documents offline (E79.3) against one ECDSA P-256 public key: the vendor's, embedded
/// in the release as <see cref="VendorPublicKey"/>, or a test pair's. A document whose signature does not
/// verify, whose payload is not a key of this schema, or whose algorithm is not ES256 is refused with a
/// reason; nothing about it is trusted, not even its key id.
/// </summary>
public sealed class LicenseVerifier : IDisposable
{
    /// <summary>
    /// The vendor's public key (SubjectPublicKeyInfo, base64). The private half never leaves the vendor.
    /// </summary>
    public const string VendorPublicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE+OMYD+tRTcPItWKI1w6gje4TOjBda7GSxkU6S9DdkYPCRRSQNBaXf4nCtex9RFj1Jc4Zs592Gg+1IT914mjxNg==";



    private readonly ECDsa _key;



    /// <summary>
    /// Creates a verifier for a public key (SubjectPublicKeyInfo, base64).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="publicKey"/> is blank.</exception>
    /// <exception cref="CryptographicException"><paramref name="publicKey"/> is not a P-256 public key.</exception>
    public LicenseVerifier(string publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey);

        _key = ECDsa.Create();
        _key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
    }



    /// <summary>
    /// The verifier for keys the vendor issued.
    /// </summary>
    public static LicenseVerifier ForVendor()
    {
        return new LicenseVerifier(VendorPublicKey);
    }



    /// <summary>
    /// Reads a document: the key when it verifies, else the reason.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    public LicenseKeyReading Read(string document)
    {
        ArgumentNullException.ThrowIfNull(document);

        LicenseKeyEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize(document, LicenseJsonContext.Default.LicenseKeyEnvelope);
        }
        catch (JsonException)
        {
            return LicenseKeyReading.Refused("the text is not a signed key document");
        }

        if (envelope?.Payload is null || envelope.Signature is null || !Base64Url.IsValid(envelope.Payload) || !Base64Url.IsValid(envelope.Signature))
        {
            return LicenseKeyReading.Refused("the text is not a signed key document");
        }

        if (!string.Equals(envelope.Algorithm, LicenseKeyEnvelope.Es256, StringComparison.Ordinal))
        {
            return LicenseKeyReading.Refused($"the key is signed with '{envelope.Algorithm}'; this release verifies ES256");
        }

        var payload = Base64Url.DecodeFromChars(envelope.Payload);
        if (!_key.VerifyData(payload, Base64Url.DecodeFromChars(envelope.Signature), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
        {
            return LicenseKeyReading.Refused("the signature does not verify: the key was not issued by the vendor or has been altered");
        }

        return ReadPayload(payload, JsonSerializer.Serialize(envelope, LicenseJsonContext.Default.LicenseKeyEnvelope));
    }



    /// <inheritdoc/>
    public void Dispose()
    {
        _key.Dispose();
    }



    private static LicenseKeyReading ReadPayload(byte[] payload, string canonical)
    {
        try
        {
            var content = JsonSerializer.Deserialize(payload, LicenseJsonContext.Default.LicenseKeyPayload);
            return content is null
                ? LicenseKeyReading.Refused("the key content is empty")
                : new LicenseKeyReading(LicenseKeyJson.FromPayload(content), canonical, Reason: null);
        }
        catch (JsonException)
        {
            return LicenseKeyReading.Refused("the key content is not valid JSON");
        }
        catch (FormatException exception)
        {
            return LicenseKeyReading.Refused(exception.Message);
        }
    }
}
