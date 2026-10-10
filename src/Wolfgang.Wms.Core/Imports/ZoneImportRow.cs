// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;
using Wolfgang.Wms.Core.Zones;

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// One zone in a zones file (E16.6): the same object as <see cref="ZoneDraft"/> plus the optional
/// <see cref="Action"/>. Resolution zones are not importable (E16.2): a row with <c>type: Resolution</c> fails.
/// </summary>
/// <param name="Code">The natural key: the zone's code, unique within the site.</param>
/// <param name="Name">The name users see.</param>
/// <param name="Type"><c>Pick</c> (the default) or <c>Bulk</c>.</param>
/// <param name="WalkOrderPrefix">The optional sortable prefix the zone's locations' walk sequences start with.</param>
/// <param name="IsRejectLane">True when a pick zone is also the conveyor's error/overflow lane.</param>
/// <param name="IsActive">False to retire the zone.</param>
/// <param name="Action"><c>Upsert</c> (the default) or <c>Delete</c>.</param>
public sealed record ZoneImportRow
(
    string Code,
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ZoneType>))] ZoneType Type,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ImportAction>))] ImportAction Action,
    string? WalkOrderPrefix = null,
    bool IsRejectLane = false,
    bool IsActive = true
)
{
    /// <summary>
    /// The row as the zones store would take it.
    /// </summary>
    public ZoneDraft ToDraft()
    {
        return new ZoneDraft(Code, Name, Type, WalkOrderPrefix, IsRejectLane, Resolution: null, IsActive);
    }
}
