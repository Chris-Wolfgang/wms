// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Web.Shared;

/// <summary>
/// An open break-glass window (E9.3), as the console banner reports it.
/// </summary>
/// <param name="UnlockedUntil">When the window ends.</param>
public sealed record LocalLoginNotice(DateTimeOffset UnlockedUntil);
