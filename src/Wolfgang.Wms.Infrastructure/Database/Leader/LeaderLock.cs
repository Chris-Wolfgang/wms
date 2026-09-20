// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.AuditTrail;

namespace Wolfgang.Wms.Infrastructure.Database.Leader;

/// <summary>
/// One row of <c>wms.leader_lock</c> (E12.6): who holds a named lock and until when. Never audited (it
/// changes every few seconds and decides nothing about the business).
/// </summary>
[NotAudited]
public sealed class LeaderLock
{
    /// <summary>Longest lock name.</summary>
    public const int NameLength = 64;

    /// <summary>Longest holder identifier.</summary>
    public const int HolderLength = 128;



    /// <summary>The server-assigned identifier.</summary>
    public long Id { get; set; }



    /// <summary>The lock name (unique).</summary>
    public string Name { get; set; } = string.Empty;



    /// <summary>The holder (machine and instance), or empty when released.</summary>
    public string Holder { get; set; } = string.Empty;



    /// <summary>When the current holder took the lock (UTC).</summary>
    public DateTimeOffset AcquiredAt { get; set; }



    /// <summary>When the lease ends unless renewed (UTC); a lock past this instant may be taken over.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
