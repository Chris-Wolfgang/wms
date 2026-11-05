// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Identity.BreakGlass;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Integrity;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// The one row of <c>core.local_login_gate</c> (E9.3): when single sign-on was first verified and by which
/// provider, and the last unlock window (who opened it from the host, until when, who closed it early).
/// Audited like every table (E6.4), so each unlock and lock is a record that names the OS user; versioned
/// (E5.1) so two hosts cannot both rewrite the window unseen; signed (E10.4) so a row edited through the
/// database cannot reopen local sign-in. Created on the first write; <see cref="Singleton"/> and its unique
/// index keep it to one row whichever host writes first.
/// </summary>
public sealed class LocalLoginGate : IVersionedEntity, ISignedEntity
{
    /// <summary>Longest provider name.</summary>
    public const int ProviderLength = 32;

    /// <summary>Longest actor name (an OS user such as <c>DOMAIN\\name</c>).</summary>
    public const int ActorLength = 256;

    /// <summary>The value of <see cref="Singleton"/>.</summary>
    public const int SingletonValue = 1;



    /// <summary>The server-assigned identifier; the row is a singleton.</summary>
    public long Id { get; set; }



    /// <summary>Always <see cref="SingletonValue"/>; its unique index refuses a second row.</summary>
    public int Singleton { get; set; } = SingletonValue;



    /// <summary>When the first external sign-in succeeded; null while SSO is unverified.</summary>
    public DateTimeOffset? SsoVerifiedAt { get; set; }



    /// <summary>The provider that verified SSO.</summary>
    public string? SsoVerifiedProvider { get; set; }



    /// <summary>End of the last unlock window.</summary>
    public DateTimeOffset? UnlockedUntil { get; set; }



    /// <summary>When the last window was opened.</summary>
    public DateTimeOffset? UnlockedAt { get; set; }



    /// <summary>The host OS user who opened the last window.</summary>
    public string? UnlockedBy { get; set; }



    /// <summary>When the last window was closed early; null when it ran out or is still open.</summary>
    public DateTimeOffset? LockedAt { get; set; }



    /// <summary>The host OS user who closed the last window early.</summary>
    public string? LockedBy { get; set; }



    /// <inheritdoc/>
    public long RowVersion { get; set; }



    /// <inheritdoc/>
    public string? Signature { get; set; }



    /// <inheritdoc/>
    public string CanonicalContent()
    {
        return string.Join('\n', "local_login_gate", SignedContent.Timestamp(SsoVerifiedAt), SsoVerifiedProvider ?? string.Empty, SignedContent.Timestamp(UnlockedUntil), UnlockedBy ?? string.Empty);
    }



    /// <summary>
    /// The row as the gate reports it.
    /// </summary>
    public LocalLoginGateInfo ToInfo()
    {
        return new LocalLoginGateInfo(SsoVerifiedAt, SsoVerifiedProvider, UnlockedUntil, UnlockedBy);
    }
}
