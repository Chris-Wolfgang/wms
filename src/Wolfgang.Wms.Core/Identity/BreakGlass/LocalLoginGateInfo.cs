// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.BreakGlass;

/// <summary>
/// What the break-glass gate knows (E9.3): whether single sign-on has been verified on this install (the
/// first successful external sign-in), and whether a timed unlock window is open. Local sign-in is open
/// until SSO is verified and closed afterwards, except inside a window opened from the host or while the
/// <c>Wms:Auth:ForceLocal</c> override is set.
/// </summary>
/// <param name="SsoVerifiedAt">When the first external sign-in succeeded; null while SSO is unverified.</param>
/// <param name="SsoVerifiedProvider">The provider that verified SSO; null while unverified.</param>
/// <param name="UnlockedUntil">End of the last unlock window; null when none was ever opened.</param>
/// <param name="UnlockedBy">Who opened the last window (the host OS user); null when none was opened.</param>
public sealed record LocalLoginGateInfo
(
    DateTimeOffset? SsoVerifiedAt,
    string? SsoVerifiedProvider,
    DateTimeOffset? UnlockedUntil,
    string? UnlockedBy
)
{
    /// <summary>
    /// The gate before anything happened: SSO unverified, no window, local sign-in open.
    /// </summary>
    public static LocalLoginGateInfo Open { get; } = new(SsoVerifiedAt: null, SsoVerifiedProvider: null, UnlockedUntil: null, UnlockedBy: null);



    /// <summary>
    /// True once an external sign-in has succeeded on this install.
    /// </summary>
    public bool IsSsoVerified => SsoVerifiedAt is not null;



    /// <summary>
    /// True while an unlock window is open at <paramref name="now"/>.
    /// </summary>
    public bool IsUnlockedAt(DateTimeOffset now)
    {
        return UnlockedUntil is { } until && until > now;
    }



    /// <summary>
    /// True when a local sign-in may proceed at <paramref name="now"/>: SSO is not verified yet, a window is
    /// open, or <paramref name="forceLocal"/> (the bootstrap override) is set.
    /// </summary>
    public bool IsOpenAt(DateTimeOffset now, bool forceLocal)
    {
        return forceLocal || !IsSsoVerified || IsUnlockedAt(now);
    }



    /// <summary>
    /// The anonymous view the login page and the console banner read.
    /// </summary>
    public LocalLoginStatus ToStatus(DateTimeOffset now, bool forceLocal)
    {
        return new LocalLoginStatus(IsOpenAt(now, forceLocal), IsSsoVerified, IsUnlockedAt(now) ? UnlockedUntil : null, forceLocal);
    }
}
