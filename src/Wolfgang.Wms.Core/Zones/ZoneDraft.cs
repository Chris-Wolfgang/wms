// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;

namespace Wolfgang.Wms.Core.Zones;

/// <summary>
/// What an administrator sends to create or replace a zone (E16.2).
/// </summary>
/// <param name="Code">The code, unique within the site: letters, digits, <c>-</c> and <c>_</c>, compared without regard to case.</param>
/// <param name="Name">The name users see.</param>
/// <param name="Type">What the zone is for.</param>
/// <param name="WalkOrderPrefix">An optional sortable prefix the zone's locations' walk sequences start with (E17.1); null for none.</param>
/// <param name="IsRejectLane">True when a pick zone is also the conveyor's error/overflow lane; drives the reject diagnostics (E26.3). Pick zones only.</param>
/// <param name="Resolution">The resolution-zone properties; required for a <see cref="ZoneType.Resolution"/> zone, absent otherwise.</param>
/// <param name="IsActive">False to retire the zone. Refused while open zone groups exist.</param>
public sealed record ZoneDraft
(
    string Code,
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ZoneType>))] ZoneType Type,
    string? WalkOrderPrefix,
    bool IsRejectLane,
    ResolutionZone? Resolution,
    bool IsActive = true
);
