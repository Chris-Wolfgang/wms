// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Docs;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E83.2, E83.3: the reference pages under <c>docfx_project/docs/reference/</c> are generated from the API
/// host's registries and must equal the committed copies (set <c>WMS_UPDATE_REFERENCE_DOCS=1</c> to rewrite
/// them); every error code has its entry on the troubleshooting page. A setting, permission or code added
/// without its docs fails here, in the pull request that adds it.
/// </summary>
public sealed class ReferenceDocsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string UpdateVariable = "WMS_UPDATE_REFERENCE_DOCS";
    private readonly WebApplicationFactory<Program> _factory;



    public ReferenceDocsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task Committed_reference_pages_match_the_generated_ones()
    {
        var modules = _factory.Services.GetRequiredService<ModuleCollection>();
        var codes = AllErrorCodes();
        var update = string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal);
        var pages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["settings.md"] = ReferencePages.Settings(modules),
            ["permissions.md"] = ReferencePages.Permissions(modules),
            ["error-codes.md"] = ReferencePages.ErrorCodes(codes),
            ["modules.md"] = ReferencePages.Modules(modules),
        };

        foreach (var (name, generated) in pages)
        {
            var path = Path.Combine(ReferenceDirectory(), name);
            var committed = await ReadOrUpdateAsync(path, generated, update);
            Assert.True(string.Equals(generated.ReplaceLineEndings("\n"), committed, StringComparison.Ordinal), $"{path} is stale; rerun with {UpdateVariable}=1 and commit the result.");
        }

        Assert.Contains("| `license.keys` | Secret |", pages["settings.md"], StringComparison.Ordinal);
        Assert.Contains("| `license.manage` | license |", pages["permissions.md"], StringComparison.Ordinal);
        Assert.Contains("| `picking.tote_missing` |", ReferencePages.ErrorCodes([new ErrorCode("picking.tote_missing", 404, "Tote {0} is not on the line.", "tote-missing", ErrorSeverity.Warning)]), StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => ReferencePages.Settings(null!));
        Assert.Throws<ArgumentNullException>(() => ReferencePages.Permissions(null!));
        Assert.Throws<ArgumentNullException>(() => ReferencePages.ErrorCodes(null!));
        Assert.Throws<ArgumentNullException>(() => ReferencePages.Modules(null!));
    }



    [Fact]
    public async Task Every_error_code_has_a_troubleshooting_entry()
    {
        var codes = AllErrorCodes();
        var page = await File.ReadAllTextAsync(Path.Combine(ReferenceDirectory(), "..", "troubleshooting.md"));

        var missing = ReferencePages.MissingTroubleshootingEntries(page, codes);
        var sample = ReferencePages.MissingTroubleshootingEntries("<a id=\"tote-missing\"></a>", [new ErrorCode("picking.tote_missing", 404, "m", "tote-missing", ErrorSeverity.Warning), new ErrorCode("picking.other", 404, "m", "other", ErrorSeverity.Warning)]);

        Assert.True(missing.Count == 0, "Troubleshooting entries missing for: " + string.Join(", ", missing.Select(c => c.Code)));
        Assert.True(codes.Count >= 30);
        Assert.Equal(["picking.other"], sample.Select(c => c.Code));
        Assert.All(codes, c => Assert.Equal(ApiProblems.DocsBase + "#" + c.DocsAnchor, ApiProblems.TypeUri(c)));
        Assert.Throws<ArgumentNullException>(() => ReferencePages.MissingTroubleshootingEntries(null!, codes));
        Assert.Throws<ArgumentNullException>(() => ReferencePages.MissingTroubleshootingEntries(page, null!));
    }



    [Fact]
    public async Task Update_mode_rewrites_a_page_with_LF_line_endings()
    {
        var path = Path.Combine(Path.GetTempPath(), "wms-reference-" + Guid.NewGuid().ToString("N"), "settings.md");

        var missing = await ReadOrUpdateAsync(path, "x", update: false);
        var written = await ReadOrUpdateAsync(path, "a" + Environment.NewLine + "b", update: true);
        var bytes = await File.ReadAllBytesAsync(path);
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);

        Assert.Equal(string.Empty, missing);
        Assert.Equal("a" + (char)10 + "b", written);
        Assert.DoesNotContain((byte)13, bytes);
    }



    /// <summary>
    /// Every code Core defines, on a module descriptor or on a <c>*ErrorCodes</c> catalog used by a filter.
    /// </summary>
    private static IReadOnlyList<ErrorCode> AllErrorCodes()
    {
        return typeof(ApiProblems).Assembly
            .GetTypes()
            .Where(t => t.IsAbstract && t.IsSealed && t.Name.EndsWith("ErrorCodes", StringComparison.Ordinal))
            .SelectMany(t => KeyDefinitions.Enumerate<ErrorCode>(t))
            .DistinctBy(c => c.Code, StringComparer.Ordinal)
            .ToList();
    }



    private static string ReferenceDirectory()
    {
        var directory = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(directory, "Wolfgang.Wms.slnx")))
        {
            directory = Path.GetDirectoryName(directory) ?? throw new InvalidOperationException("Repository root not found.");
        }

        return Path.Combine(directory, "docfx_project", "docs", "reference");
    }



    private static async Task<string> ReadOrUpdateAsync(string path, string generated, bool update)
    {
        if (update)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, generated.ReplaceLineEndings("\n"));
        }

        return File.Exists(path) ? (await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n") : string.Empty;
    }
}
