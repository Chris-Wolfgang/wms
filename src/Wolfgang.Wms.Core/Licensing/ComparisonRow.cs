// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// One row of the feature comparison (E79.10): a feature or a limit and its cell per tier.
/// </summary>
/// <param name="Name">The feature or limit name.</param>
/// <param name="Description">Its description.</param>
/// <param name="Area">The area the name is grouped under (the part before the first dot).</param>
/// <param name="Cells">One cell per tier, in the table's tier order: "✓" or "—" for a feature, the number or "unlimited" for a limit.</param>
public sealed record ComparisonRow(string Name, string Description, string Area, IReadOnlyList<string> Cells);
