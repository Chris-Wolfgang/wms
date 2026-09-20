// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Licensing;
using Wolfgang.Wms.Domain.Licensing;
using Wolfgang.Wms.UnitTests.Architecture;

namespace Wolfgang.Wms.UnitTests.Licensing;

/// <summary>
/// E79.1, E79.10: the CI checks. The committed tier table (<c>docs/licensing/tier-table.txt</c>, the last
/// released one, refreshed by the release cut) must not be regressed by this release's; the committed
/// comparison page must be the one generated from this release's table, with what changed since the
/// committed one. Set <c>WMS_UPDATE_LICENSING_DOCS=1</c> to rewrite the page.
/// </summary>
public sealed class FeatureComparisonTests
{
    private const string UpdateVariable = "WMS_UPDATE_LICENSING_DOCS";
    private static readonly string TableFile = Path.Combine(RepositoryFiles.Root, "docs", "licensing", "tier-table.txt");
    private static readonly string ComparisonFile = Path.Combine(RepositoryFiles.Root, "docfx_project", "docs", "licensing", "feature-comparison.md");



    [Fact]
    public async Task This_release_only_adds_to_the_committed_table_and_the_page_is_current()
    {
        var update = string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal);
        var committedLines = File.Exists(TableFile) ? await File.ReadAllLinesAsync(TableFile) : [];
        var committed = TierTable.FromLines("committed", committedLines);
        var current = TierTables.Current;
        var page = FeatureComparisonBuilder.ToMarkdown(FeatureComparisonBuilder.Build(current), TierTable.Additions(committed, current));
        await WriteIfUpdatingAsync(ComparisonFile, page, update);

        Assert.Empty(current.Verify());
        Assert.Empty(TierTable.Regressions(committed, current));
        Assert.True(File.Exists(ComparisonFile), $"{ComparisonFile} is missing; run the test with {UpdateVariable}=1");
        Assert.Equal(page.ReplaceLineEndings(), (await File.ReadAllTextAsync(ComparisonFile)).ReplaceLineEndings());
    }



    [Fact]
    public async Task Update_mode_rewrites_the_page()
    {
        var path = Path.Combine(Path.GetTempPath(), "wms-licensing-" + Guid.NewGuid().ToString("N"), "feature-comparison.md");

        await WriteIfUpdatingAsync(path, "untouched", update: false);
        var untouched = File.Exists(path);
        await WriteIfUpdatingAsync(path, "written", update: true);
        var written = await File.ReadAllTextAsync(path);
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);

        Assert.False(untouched);
        Assert.Equal("written", written);
    }



    [Fact]
    public void The_comparison_marks_features_limits_and_the_installed_tier()
    {
        var comparison = FeatureComparisonBuilder.Build(TierTables.Current, LicenseTiers.Pro);
        var insights = comparison.Features.Single(r => string.Equals(r.Name, "workspace.insights", StringComparison.Ordinal));
        var devices = comparison.Limits.Single(r => string.Equals(r.Name, "devices", StringComparison.Ordinal));
        var users = comparison.Limits.Single(r => string.Equals(r.Name, "users", StringComparison.Ordinal));
        var page = FeatureComparisonBuilder.ToMarkdown(comparison, ["tier 'free' gains 'x'"]);

        Assert.Equal(("pro", ReleaseInfo.Version), (comparison.InstalledTier, comparison.Version));
        Assert.Equal(["free", "pro", "enterprise"], comparison.Tiers);
        Assert.Equal(["—", "✓", "✓"], insights.Cells);
        Assert.Equal(("workspace", "The Insights console workspace"), (insights.Area, insights.Description));
        Assert.Equal(["5", "5", "5"], devices.Cells);
        Assert.Equal(["unlimited", "unlimited", "unlimited"], users.Cells);
        Assert.Equal("self_update", comparison.Features.Single(r => string.Equals(r.Name, "self_update", StringComparison.Ordinal)).Area);
        Assert.Contains("| Feature | Free | Pro | Enterprise |", page, StringComparison.Ordinal);
        Assert.Contains("| **workspace** | | | |", page, StringComparison.Ordinal);
        Assert.Contains("| Totes per picker (`max_totes_per_picker`) | 1 | 5 | unlimited |", page, StringComparison.Ordinal);
        Assert.Contains("- tier 'free' gains 'x'", page, StringComparison.Ordinal);
        Assert.Contains("Nothing: the table is the same", FeatureComparisonBuilder.ToMarkdown(comparison, []), StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => FeatureComparisonBuilder.Build(null!));
        Assert.Throws<ArgumentNullException>(() => FeatureComparisonBuilder.ToMarkdown(null!, []));
        Assert.Throws<ArgumentNullException>(() => FeatureComparisonBuilder.ToMarkdown(comparison, null!));
    }



    private static async Task WriteIfUpdatingAsync(string path, string page, bool update)
    {
        if (update)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, page);
        }
    }
}
