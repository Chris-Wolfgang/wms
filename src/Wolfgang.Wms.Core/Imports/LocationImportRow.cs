// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;
using Wolfgang.Wms.Core.Locations;

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// One location in a locations file (E16.6): the same object as <see cref="LocationDraft"/> except that the
/// zone is named by its code (the natural key an ERP or a spreadsheet knows), plus the optional
/// <see cref="Action"/>.
/// </summary>
/// <param name="Code">The natural key: the bin code, unique within the site.</param>
/// <param name="Barcode">The label on the bin, unique within the site.</param>
/// <param name="ZoneCode">The code of a zone of the site; it must already exist.</param>
/// <param name="WalkSequence">The sortable walk order; starts with the zone's walk-order prefix when it has one.</param>
/// <param name="IsPickable">False for a bin never picked from.</param>
/// <param name="IsActive">False to retire the bin.</param>
/// <param name="Action"><c>Upsert</c> (the default) or <c>Delete</c>.</param>
public sealed record LocationImportRow
(
    string Code,
    string Barcode,
    string ZoneCode,
    string WalkSequence,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ImportAction>))] ImportAction Action,
    bool IsPickable = true,
    bool IsActive = true
)
{
    /// <summary>
    /// The row as the locations store would take it, once the zone code resolved to <paramref name="zoneId"/>.
    /// </summary>
    public LocationDraft ToDraft(long zoneId)
    {
        return new LocationDraft(Code, Barcode, zoneId, WalkSequence, IsPickable, IsActive);
    }
}
