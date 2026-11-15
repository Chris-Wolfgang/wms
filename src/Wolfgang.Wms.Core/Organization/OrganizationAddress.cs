// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Organization;

/// <summary>
/// The organisation's postal address (E16.0), as printed on cards and digests.
/// </summary>
/// <param name="Line1">Street and number.</param>
/// <param name="Line2">Second line (suite, building); null when none.</param>
/// <param name="City">City or town.</param>
/// <param name="Region">State, province or county; null when the country has none.</param>
/// <param name="PostalCode">Postal or ZIP code.</param>
/// <param name="Country">Country, as written on an envelope.</param>
public sealed record OrganizationAddress(string Line1, string? Line2, string City, string? Region, string PostalCode, string Country);
