// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// Per-user "sessions valid after" (E10.5): a session or token issued before this instant is invalid. Set
/// on password change, disable and role change so those take effect immediately, checked on every request
/// that presents a session.
/// </summary>
public interface ISessionRevocations
{
    /// <summary>
    /// The instant before which <paramref name="userId"/>'s sessions are invalid, null when none is set, or
    /// <see cref="DateTimeOffset.MaxValue"/> when the user no longer exists or is disabled (every session invalid).
    /// </summary>
    Task<DateTimeOffset?> SessionsValidAfterAsync(long userId, CancellationToken cancellationToken);



    /// <summary>
    /// Invalidates every session and token of <paramref name="userId"/> issued before now.
    /// </summary>
    Task RevokeSessionsAsync(long userId, CancellationToken cancellationToken);
}
