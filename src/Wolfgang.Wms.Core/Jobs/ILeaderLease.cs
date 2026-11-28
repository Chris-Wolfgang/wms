// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Jobs;

/// <summary>
/// A held lock (E12.6). <see cref="Lost"/> is cancelled when a renewal fails (the database was away
/// longer than the lease, another instance took over): the job stops its work at the next check.
/// </summary>
public interface ILeaderLease : IAsyncDisposable
{
    /// <summary>
    /// The lock name.
    /// </summary>
    string Name { get; }



    /// <summary>
    /// Cancelled when the lease can no longer be renewed.
    /// </summary>
    CancellationToken Lost { get; }
}
