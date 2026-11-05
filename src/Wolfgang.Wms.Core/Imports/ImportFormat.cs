// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// One entity type's file format (E16.6), as the generated reference page documents it: the route, the
/// natural key, the default bad-row policy and the fields of the row type the API deserializes.
/// </summary>
/// <param name="Entity">The entity type (<c>zones</c>, <c>locations</c>).</param>
/// <param name="RowType">The row record the JSON array carries.</param>
/// <param name="Route">The import endpoint.</param>
/// <param name="NaturalKey">The field the upsert matches on.</param>
/// <param name="DefaultPolicy">The policy used when the request names none.</param>
/// <param name="Notes">The rules that go beyond the fields.</param>
/// <param name="Fields">The fields, in the row type's order.</param>
public sealed record ImportFormat
(
    string Entity,
    Type RowType,
    string Route,
    string NaturalKey,
    ImportPolicy DefaultPolicy,
    string Notes,
    IReadOnlyList<ImportField> Fields
);
