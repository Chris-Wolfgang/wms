// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// Body of <c>POST /auth/local/password</c> (E9.1, E9.2).
/// </summary>
/// <param name="CurrentPassword">The password in use.</param>
/// <param name="NewPassword">The replacement; must satisfy <see cref="PasswordPolicy"/>.</param>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
