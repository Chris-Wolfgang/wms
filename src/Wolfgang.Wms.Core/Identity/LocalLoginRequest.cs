// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// Body of <c>POST /auth/local/login</c> (E9.2).
/// </summary>
/// <param name="UserName">The sign-in name.</param>
/// <param name="Password">The password.</param>
public sealed record LocalLoginRequest(string UserName, string Password);
