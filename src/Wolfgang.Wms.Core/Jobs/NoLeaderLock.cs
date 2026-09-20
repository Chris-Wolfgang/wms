// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Jobs;

/// <summary>
/// The lock of a host without a database (E12.6): there is one instance, and it always leads.
/// </summary>
public sealed class NoLeaderLock : ILeaderLock
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public Task<ILeaderLease?> TryAcquireAsync(string name, TimeSpan lease, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Task.FromResult<ILeaderLease?>(new AlwaysHeld(name));
    }



    private sealed class AlwaysHeld : ILeaderLease
    {
        public AlwaysHeld(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public CancellationToken Lost => CancellationToken.None;

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
