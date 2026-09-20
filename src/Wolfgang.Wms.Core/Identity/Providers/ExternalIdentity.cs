// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// What a challenge provider knows about the signed-in person (E11.0).
/// </summary>
/// <param name="Subject">The provider's stable identifier for the person (never the e-mail).</param>
/// <param name="UserName">The sign-in name shown in the console.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Groups">The directory group identifiers, for role mapping (E11.2).</param>
public sealed record ExternalIdentity(string Subject, string UserName, string DisplayName, IReadOnlyList<string> Groups);
