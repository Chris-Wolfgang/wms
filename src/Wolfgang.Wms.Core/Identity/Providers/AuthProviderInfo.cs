// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// One enabled provider as the login page sees it (E11.0).
/// </summary>
/// <param name="Name">The provider name.</param>
/// <param name="DisplayName">The button label.</param>
/// <param name="Kind">Credentials (a form) or Challenge (a redirect).</param>
/// <param name="ChallengeUrl">Where the browser goes for a challenge provider; null for a credentials one.</param>
public sealed record AuthProviderInfo
(
    string Name,
    string DisplayName,
    [property: JsonConverter(typeof(JsonStringEnumConverter<AuthProviderKind>))] AuthProviderKind Kind,
    string? ChallengeUrl
);
