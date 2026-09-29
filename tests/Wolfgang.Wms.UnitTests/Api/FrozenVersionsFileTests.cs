// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.UnitTests.Architecture;

namespace Wolfgang.Wms.UnitTests.Api;

/// <summary>
/// E82.2: <c>docs/api/frozen.txt</c> is what the CI OpenAPI diff reads; <see cref="WmsApi.Frozen"/> is what the
/// code serves. They must name the same versions, or CI would protect a contract the host does not freeze.
/// </summary>
public sealed class FrozenVersionsFileTests
{
    private const string FrozenFile = "docs/api/frozen.txt";



    [Fact]
    public void Frozen_versions_file_matches_WmsApi_Frozen()
    {
        var path = Path.Combine(RepositoryFiles.Root, FrozenFile);

        Assert.True(File.Exists(path), $"{FrozenFile} is missing");
        Assert.Equal(WmsApi.Frozen.Select(WmsApi.DocumentName), FrozenDocumentNamesIn(File.ReadAllLines(path)));
    }



    [Fact]
    public void Every_frozen_version_is_also_served()
    {
        Assert.All(WmsApi.Frozen, version => Assert.Contains(version, WmsApi.Served));
    }



    [Fact]
    public void Frozen_file_parser_ignores_comments_and_blank_lines()
    {
        var names = FrozenDocumentNamesIn(["# comment", "", "v1 ", "  ", "v2"]);

        Assert.Equal(["v1", "v2"], names);
    }



    private static List<string> FrozenDocumentNamesIn(IEnumerable<string> lines)
    {
        return lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();
    }
}
