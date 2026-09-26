// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.AuditTrail;

namespace Wolfgang.Wms.Infrastructure.Database.Auditing;

/// <summary>
/// The user provider outside a host (E6.4): design time, the migrate tool and tests, which record
/// <see cref="WmsAuditing.SystemIdentity"/> with nobody on whose behalf.
/// </summary>
public sealed class SystemAuditUserProvider : IAuditUserProvider
{
    /// <summary>
    /// The shared instance.
    /// </summary>
    public static SystemAuditUserProvider Instance { get; } = new();



    /// <inheritdoc/>
    public AuditUser GetCurrentUser()
    {
        return new AuditUser(WmsAuditing.SystemIdentity);
    }
}
