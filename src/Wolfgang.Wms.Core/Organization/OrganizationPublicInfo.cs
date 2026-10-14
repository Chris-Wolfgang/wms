// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Organization;

/// <summary>
/// The organisation as the login page shows it before anyone is signed in (E16.0): the name and the logo,
/// nothing that identifies people or places.
/// </summary>
/// <param name="Name">The name users see.</param>
/// <param name="LogoDataUrl">The logo as a <c>data:image/…</c> URL; null for none.</param>
public sealed record OrganizationPublicInfo(string Name, string? LogoDataUrl);
