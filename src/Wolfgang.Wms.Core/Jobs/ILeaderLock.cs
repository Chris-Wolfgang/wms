// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Jobs;

/// <summary>
/// A named lock one instance holds at a time (E12.6): singleton worker jobs (verification, outbox, lease
/// expiry, rollups, retention, ingest) run under it, so any number of workers can run and exactly one
/// executes each job. A lease expires unless renewed; a crashed holder is taken over after the lease ends.
/// </summary>
public interface ILeaderLock
{
    /// <summary>
    /// Tries to take the lock for <paramref name="lease"/>; null when another live instance holds it. The
    /// returned lease renews itself in the background and is released when disposed.
    /// </summary>
    Task<ILeaderLease?> TryAcquireAsync(string name, TimeSpan lease, CancellationToken cancellationToken);
}
