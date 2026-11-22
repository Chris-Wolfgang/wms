// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The signed document an administrator pastes (E79.3): the payload bytes (base64url of the payload JSON),
/// the signature over exactly those bytes (base64url, ES256 in IEEE P1363 form) and the algorithm name.
/// The signature covers the encoded bytes, so no canonicalisation is needed on either side.
/// </summary>
internal sealed record LicenseKeyEnvelope
{
    /// <summary>The only algorithm this release signs and verifies with.</summary>
    public const string Es256 = "ES256";

    /// <summary>Base64url of the payload JSON.</summary>
    public string? Payload { get; init; }

    /// <summary>Base64url of the signature.</summary>
    public string? Signature { get; init; }

    /// <summary>The algorithm name.</summary>
    public string? Algorithm { get; init; }
}
