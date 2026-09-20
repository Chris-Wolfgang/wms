// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// Body of <c>POST /auth/roles</c> and <c>PUT /auth/roles/{id}</c> (E10.2).
/// </summary>
/// <param name="Name">The role name, unique (case-insensitive), 1–128 characters.</param>
/// <param name="Description">What the role is for.</param>
/// <param name="Permissions">Catalog permission names the role grants.</param>
public sealed record RoleDraft(string Name, string Description, IReadOnlyList<string> Permissions);
