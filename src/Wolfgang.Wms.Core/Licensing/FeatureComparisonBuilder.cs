// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using System.Text;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// Builds the feature comparison from a tier table (E79.10) and renders it as the Markdown page the docs
/// publish per version, with the release's "what changed" (additions only, by the monotonic rule).
/// </summary>
public static class FeatureComparisonBuilder
{
    /// <summary>The cell for a granted feature.</summary>
    public const string Granted = "✓";

    /// <summary>The cell for a feature the tier lacks.</summary>
    public const string NotGranted = "—";



    /// <summary>
    /// The comparison for <paramref name="table"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="table"/> is null.</exception>
    public static FeatureComparison Build(TierTable table, LicenseTier? installed = null)
    {
        ArgumentNullException.ThrowIfNull(table);

        var tiers = table.Tiers.Select(t => t.Tier.Name).ToList();
        var features = LicenseFeatures.All
            .Select(f => new ComparisonRow(f.Name, f.Description, Area(f.Name), table.Tiers.Select(t => t.Features.Contains(f.Name) ? Granted : NotGranted).ToList()))
            .ToList();
        var limits = LicenseLimits.All
            .Select(l => new ComparisonRow(l.Name, l.Description, Area(l.Name), table.Tiers.Select(t => t.Limit(l).ToString()).ToList()))
            .ToList();
        return new FeatureComparison(table.Version, tiers, features, limits, installed?.Name);
    }



    /// <summary>
    /// The Markdown page: the feature table grouped by area, the limits table, and what changed.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static string ToMarkdown(FeatureComparison comparison, IReadOnlyList<string> changes)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(changes);

        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"# Feature comparison — {comparison.Version}\n\n");
        text.Append("Generated from this release's tier table (`TierTables.Current`) by the `FeatureComparisonTests` test; do not edit by hand.\n\n");
        text.Append("## Features\n\n");
        AppendHeader(text, comparison.Tiers, "Feature");
        foreach (var group in comparison.Features.GroupBy(r => r.Area, StringComparer.Ordinal))
        {
            text.Append(CultureInfo.InvariantCulture, $"| **{group.Key}** |{string.Concat(comparison.Tiers.Select(_ => " |"))}\n");
            foreach (var row in group)
            {
                text.Append(CultureInfo.InvariantCulture, $"| {row.Description} (`{row.Name}`) | {string.Join(" | ", row.Cells)} |\n");
            }
        }

        text.Append("\n## Limits\n\n");
        AppendHeader(text, comparison.Tiers, "Limit");
        foreach (var row in comparison.Limits)
        {
            text.Append(CultureInfo.InvariantCulture, $"| {row.Description} (`{row.Name}`) | {string.Join(" | ", row.Cells)} |\n");
        }

        text.Append("\n## What changed in this release\n\n");
        text.Append(changes.Count == 0 ? "Nothing: the table is the same as the previous release's.\n" : string.Concat(changes.Select(c => $"- {c}\n")));
        return text.ToString();
    }



    private static void AppendHeader(StringBuilder text, IReadOnlyList<string> tiers, string first)
    {
        text.Append(CultureInfo.InvariantCulture, $"| {first} | {string.Join(" | ", tiers.Select(Title))} |\n");
        text.Append(CultureInfo.InvariantCulture, $"|---|{string.Concat(tiers.Select(_ => "---|"))}\n");
    }



    private static string Title(string name)
    {
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }



    private static string Area(string name)
    {
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? name : name[..dot];
    }
}
