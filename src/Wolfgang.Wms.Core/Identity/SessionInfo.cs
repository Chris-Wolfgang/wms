// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// Who is signed in (<c>GET /auth/me</c>, E9).
/// </summary>
/// <param name="UserId">The user's id.</param>
/// <param name="UserName">The sign-in name.</param>
/// <param name="DisplayName">The name shown on screen.</param>
/// <param name="MustChangePassword">True while the password must be replaced before anything else.</param>
/// <param name="IsLocalAdmin">True for the break-glass administrator.</param>
/// <param name="Permissions">The permission grants the session carries (<c>name@organization</c>, <c>name@site:3</c>, <c>*@organization</c>).</param>
public sealed record SessionInfo(long UserId, string UserName, string DisplayName, bool MustChangePassword, bool IsLocalAdmin, IReadOnlyList<string> Permissions);
