// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Integrity;

/// <summary>
/// A security-critical row that carries an HMAC over its content (E10.4): users, roles, assignments, and
/// later API keys, device tokens, the license key and the admin-unlock state. The application signs on
/// every save and verifies before honouring the row; a row edited through the database without the key
/// fails verification and is not honoured.
/// </summary>
public interface ISignedEntity
{
    /// <summary>
    /// The server-assigned identifier. The signature covers it together with <see cref="CanonicalContent"/>
    /// (<see cref="IntegritySigner.Message"/>), so a row's content and signature cannot be moved to another id:
    /// swapping two users would otherwise put a known password on the id that holds the grants. A new row is
    /// signed right after its insert, once the id exists (<see cref="IntegritySigningInterceptor"/>).
    /// </summary>
    long Id { get; }



    /// <summary>
    /// The signature over <see cref="CanonicalContent"/>, base64; null on a row written before signing existed.
    /// </summary>
    string? Signature { get; set; }



    /// <summary>
    /// The security-relevant content in a stable form: the fields whose change would alter a decision,
    /// joined with newlines in a fixed order, invariant culture.
    /// </summary>
    string CanonicalContent();
}
