// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// One row of an import result (E16.6): the same shape in the API, the console and the downloadable CSV.
/// </summary>
/// <param name="Row">The row's position in the file, starting at 1.</param>
/// <param name="Key">The row's natural key (zone code, bin code); blank when the row had none.</param>
/// <param name="Outcome">What happened, or would have happened.</param>
/// <param name="Code">The error code when the row failed; null otherwise.</param>
/// <param name="Message">Why the row failed; null otherwise.</param>
public sealed record ImportRowResult
(
    int Row,
    string Key,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ImportRowOutcome>))] ImportRowOutcome Outcome,
    string? Code,
    string? Message
);
