// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// One field of an import row type, as the generated reference page documents it (E16.6).
/// </summary>
/// <param name="Name">The JSON property name (also the CSV column name, E24.2).</param>
/// <param name="Type">The value type as the docs show it (<c>string</c>, <c>boolean</c>, <c>Pick | Bulk</c>).</param>
/// <param name="Required">True when a row without it fails.</param>
/// <param name="Default">The value an absent optional field takes; null when the field is required.</param>
/// <param name="Notes">What the field means and the rule it must satisfy.</param>
public sealed record ImportField
(
    string Name,
    string Type,
    bool Required,
    string? Default,
    string Notes
);
