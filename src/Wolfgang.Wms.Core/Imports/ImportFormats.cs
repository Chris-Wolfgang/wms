// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Core.Zones;

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// The file formats the importer accepts (E16.6), the source of the generated reference page. A unit test
/// keeps each format's fields equal to its row type's constructor parameters, so the page cannot drift from
/// the models the API deserializes.
/// </summary>
public static class ImportFormats
{
    /// <summary>The zones file.</summary>
    public static ImportFormat Zones { get; } = new
    (
        "zones",
        typeof(ZoneImportRow),
        ImportsModule.ZonesRoute,
        "code",
        ImportsModule.ZonesDefaultPolicy,
        "Resolution zones are not importable (type Resolution fails the row); they are created in the console or the API. A reject lane is a pick zone only. Retiring a zone (isActive false or action Delete) is refused while zone groups are open in it.",
        [
            new ImportField("code", "string", Required: true, Default: null, $"The natural key: 1–{ZoneRules.CodeLength} letters, digits, '-' and '_', unique within the site without regard to case."),
            new ImportField("name", "string", Required: true, Default: null, $"1–{ZoneRules.NameLength} characters."),
            new ImportField("type", "Pick | Bulk", Required: false, "Pick", "What the zone is for."),
            new ImportField("action", "Upsert | Delete", Required: false, "Upsert", "Delete retires the zone with that code; the code must exist."),
            new ImportField("walkOrderPrefix", "string", Required: false, "null", $"At most {ZoneRules.WalkOrderPrefixLength} characters; the sortable prefix the zone's locations' walk sequences start with."),
            new ImportField("isRejectLane", "boolean", Required: false, "false", "Pick zones only: the conveyor's error/overflow lane."),
            new ImportField("isActive", "boolean", Required: false, "true", "False retires the zone."),
        ]
    );

    /// <summary>The locations file.</summary>
    public static ImportFormat Locations { get; } = new
    (
        "locations",
        typeof(LocationImportRow),
        ImportsModule.LocationsRoute,
        "code",
        ImportsModule.LocationsDefaultPolicy,
        "zoneCode must name an existing zone of the site (load the zones file first). The barcode is unique within the site: a row whose barcode another bin holds fails. The walk sequence must start with the zone's walk-order prefix when the zone has one.",
        [
            new ImportField("code", "string", Required: true, Default: null, $"The natural key: the bin code, 1–{LocationRules.CodeLength} letters, digits, '-' and '_', unique within the site without regard to case."),
            new ImportField("barcode", "string", Required: true, Default: null, $"1–{LocationRules.BarcodeLength} visible ASCII characters without spaces or '|'; unique within the site."),
            new ImportField("zoneCode", "string", Required: true, Default: null, "The code of a zone of the site."),
            new ImportField("walkSequence", "string", Required: true, Default: null, $"1–{LocationRules.WalkSequenceLength} visible ASCII characters without spaces or '|'; the sortable walk order."),
            new ImportField("action", "Upsert | Delete", Required: false, "Upsert", "Delete retires the bin with that code; the code must exist."),
            new ImportField("isPickable", "boolean", Required: false, "true", "False for a bin never picked from."),
            new ImportField("isActive", "boolean", Required: false, "true", "False retires the bin."),
        ]
    );



    /// <summary>Every format, in dependency order (zones before locations).</summary>
    public static IReadOnlyList<ImportFormat> All { get; } = [Zones, Locations];
}
