// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// One limit on the license page (E79.6): the licensed value, the count now, and the standing.
/// </summary>
/// <param name="Name">The limit name.</param>
/// <param name="Description">Its description.</param>
/// <param name="Ceiling">The licensed value, or null when unlimited.</param>
/// <param name="Count">The count now.</param>
/// <param name="Percent">Count as a percentage of the ceiling, or null when unlimited.</param>
/// <param name="Warning">True when the count has reached the warning threshold.</param>
/// <param name="Outcome">The standing: allowed, within the allowance (grace running), or blocked.</param>
/// <param name="GraceEndsOn">The last day of grace while over, else null.</param>
/// <param name="Message">The banner while over, else empty.</param>
public sealed record LimitUsage
(
    string Name,
    string Description,
    int? Ceiling,
    int Count,
    int? Percent,
    bool Warning,
    [property: JsonConverter(typeof(JsonStringEnumConverter<LimitOutcome>))] LimitOutcome Outcome,
    DateOnly? GraceEndsOn,
    string Message
);
