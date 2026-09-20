// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// Body of <c>POST /auth/roles/{id}/copy</c> (E10.2).
/// </summary>
/// <param name="Name">The new role's name.</param>
public sealed record CopyRoleRequest(string Name);
