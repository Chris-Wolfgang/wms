// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Organization;

/// <summary>
/// A named contact of the organisation (E16.0): the primary contact the vendor and the install talk to, and
/// the support contact users are pointed at.
/// </summary>
/// <param name="Name">The person or team.</param>
/// <param name="Email">Their e-mail address.</param>
/// <param name="Phone">Their phone number; null when none.</param>
public sealed record OrganizationContact(string Name, string Email, string? Phone);
