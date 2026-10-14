// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.AuditTrail;

namespace Wolfgang.Wms.Infrastructure.Integrity;

/// <summary>
/// The one row of <c>wms.integrity_key</c> (E10.4): the HMAC key, random per installation, stored protected
/// by the Data Protection ring so only the application (any instance sharing the ring) can read it. Never
/// audited: its ciphertext has no business in the audit trail.
/// </summary>
[NotAudited]
public sealed class IntegrityKey
{
    /// <summary>
    /// The server-assigned identifier (there is one row).
    /// </summary>
    public long Id { get; set; }



    /// <summary>
    /// The key material in the <c>enc:v1:</c> form.
    /// </summary>
    public string ProtectedKey { get; set; } = string.Empty;



    /// <summary>
    /// When the key was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
