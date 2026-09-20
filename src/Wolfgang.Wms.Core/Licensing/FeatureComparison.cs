// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The feature-by-tier table for a release (E79.10), generated from the same tier table the enforcement
/// uses, so it can never disagree with what the product does.
/// </summary>
/// <param name="Version">The release.</param>
/// <param name="Tiers">The tier names across the top, lowest first.</param>
/// <param name="Features">Feature rows, in catalogue order.</param>
/// <param name="Limits">Limit rows, in catalogue order.</param>
/// <param name="InstalledTier">The tier in force here, for the console to highlight; null on the docs page.</param>
public sealed record FeatureComparison(string Version, IReadOnlyList<string> Tiers, IReadOnlyList<ComparisonRow> Features, IReadOnlyList<ComparisonRow> Limits, string? InstalledTier);
