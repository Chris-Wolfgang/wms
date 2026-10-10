// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Http;

/// <summary>
/// Endpoint metadata that turns response compression off for that endpoint whatever the request scheme
/// (BREACH: TLS may end at a reverse proxy, so Kestrel's scheme says nothing about what the client sees). Added
/// by <see cref="WmsCompression.DisableResponseCompression{TBuilder}"/>.
/// </summary>
public sealed class DisableResponseCompressionMetadata
{
    private DisableResponseCompressionMetadata()
    {
    }



    /// <summary>
    /// The single instance; the metadata carries no state.
    /// </summary>
    public static DisableResponseCompressionMetadata Instance { get; } = new();
}
