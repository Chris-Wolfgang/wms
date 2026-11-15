// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// Endpoint metadata: <paramref name="Permission"/> must be held organization-wide on this endpoint, whatever
/// site the request names (E16.1). A route without <c>siteId</c> otherwise takes its site from the
/// <c>X-Wms-Site</c> header, so a grant at one site could pass a check on a whole-organization operation.
/// </summary>
/// <param name="Permission">The permission this applies to.</param>
public sealed record OrganizationScopeMetadata(Permission Permission);
