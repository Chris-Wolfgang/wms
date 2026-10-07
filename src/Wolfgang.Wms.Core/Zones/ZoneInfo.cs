// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;

namespace Wolfgang.Wms.Core.Zones;

/// <summary>
/// A zone as the API reports it (E16.2): a picking area, bulk storage or resolution zone within a site.
/// </summary>
/// <param name="Id">The server-assigned identifier.</param>
/// <param name="SiteId">The site the zone belongs to.</param>
/// <param name="Code">The code, unique within the site.</param>
/// <param name="Name">The name users see.</param>
/// <param name="Type">What the zone is for.</param>
/// <param name="WalkOrderPrefix">The sortable prefix of the zone's locations' walk sequences; null for none.</param>
/// <param name="IsRejectLane">True when the pick zone is also the conveyor's error/overflow lane.</param>
/// <param name="Resolution">The resolution-zone properties; null for pick and bulk zones.</param>
/// <param name="IsActive">False once the zone is retired.</param>
/// <param name="UpdatedAt">When the row was last written (UTC).</param>
/// <param name="UpdatedBy">Who last wrote the row.</param>
/// <param name="RowVersion">The row's version (E5.1); the <see cref="Etag"/> is derived from it.</param>
/// <param name="CopiedFromId">The id of the row this one was copied from (E16.5); null when created outright.</param>
public sealed record ZoneInfo
(
    long Id,
    long SiteId,
    string Code,
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ZoneType>))] ZoneType Type,
    string? WalkOrderPrefix,
    bool IsRejectLane,
    ResolutionZone? Resolution,
    bool IsActive,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long RowVersion,
    long? CopiedFromId = null
)
{
    /// <summary>The entity tag a client sends back in <c>If-Match</c>.</summary>
    public string Etag => Http.EntityTag.FromRowVersion((ulong)RowVersion).Value;
}
