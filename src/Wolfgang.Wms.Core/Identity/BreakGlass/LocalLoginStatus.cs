// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.BreakGlass;

/// <summary>
/// Whether local sign-in is open (<c>GET /auth/local/status</c>, E9.3): read anonymously by the login page
/// (to hide or show the password form) and by the console (to show the unlock banner). Carries no user
/// names.
/// </summary>
/// <param name="LocalLoginOpen">True when a local sign-in may proceed right now.</param>
/// <param name="SsoVerified">True once an external sign-in has succeeded on this install.</param>
/// <param name="UnlockedUntil">End of the open unlock window; null when no window is open.</param>
/// <param name="ForcedLocal">True when <c>Wms:Auth:ForceLocal</c> keeps local sign-in open regardless.</param>
public sealed record LocalLoginStatus(bool LocalLoginOpen, bool SsoVerified, DateTimeOffset? UnlockedUntil, bool ForcedLocal);
