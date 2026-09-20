// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// One installed key on the license page (E79.11): what it adds, its coverage and its status.
/// </summary>
/// <param name="KeyId">The key id (or the line, for a document that could not be read).</param>
/// <param name="Kind">Base or add-on; null when unreadable.</param>
/// <param name="Tier">The tier of a base key.</param>
/// <param name="Organization">The organization the key binds to.</param>
/// <param name="Devices">Devices the key adds.</param>
/// <param name="Features">Features the key grants explicitly.</param>
/// <param name="CoverageFrom">The first covered day, or null when perpetual or co-terminating.</param>
/// <param name="CoverageTo">The last covered day, or null when perpetual or co-terminating.</param>
/// <param name="IssuedAt">When the key was issued.</param>
/// <param name="Status">Its status.</param>
/// <param name="Reason">Why, for a status other than active.</param>
/// <param name="Summary">One line: "Pro base, covered to 2027-03-31" or "+10 devices".</param>
public sealed record InstalledKeyInfo
(
    string KeyId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<LicenseKeyKind>))] LicenseKeyKind? Kind,
    string? Tier,
    string? Organization,
    int Devices,
    IReadOnlyList<string> Features,
    DateOnly? CoverageFrom,
    DateOnly? CoverageTo,
    DateOnly? IssuedAt,
    [property: JsonConverter(typeof(JsonStringEnumConverter<KeyStatus>))] KeyStatus Status,
    string? Reason,
    string Summary
);
