// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Http;

/// <summary>
/// Endpoint metadata that turns response compression off over TLS for that endpoint (BREACH). Added by
/// <see cref="WmsCompression.DisableResponseCompression{TBuilder}"/>.
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
