// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// What <see cref="LicenseVerifier.Read"/> made of a document (E79.3): the key when the signature checked
/// and the content is valid, else the reason. <see cref="Document"/> is the compact canonical form to store.
/// </summary>
/// <param name="Key">The verified key, or null.</param>
/// <param name="Document">The document in its compact form (null when it could not be parsed).</param>
/// <param name="Reason">Why the document was refused, for a null key.</param>
public sealed record LicenseKeyReading(LicenseKey? Key, string? Document, string? Reason)
{
    /// <summary>
    /// A refusal.
    /// </summary>
    public static LicenseKeyReading Refused(string reason)
    {
        return new LicenseKeyReading(Key: null, Document: null, reason);
    }
}
