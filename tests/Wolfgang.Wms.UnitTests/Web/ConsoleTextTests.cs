// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Wolfgang.Wms.Domain.Localization;
using Wolfgang.Wms.UnitTests.Architecture;
using Wolfgang.Wms.Web.Shared;

namespace Wolfgang.Wms.UnitTests.Web;

/// <summary>
/// <c>ConsoleText.resx</c> holds exactly the keys the console uses (E82.4): every <c>L["…"]</c> in a console
/// component and every workspace title and description resolves, no key is left unused, and the resource is
/// embedded where the localizer looks for it.
/// </summary>
public sealed class ConsoleTextTests
{
    private const string ResxPath = "src/Wolfgang.Wms.Web.Shared/Resources/ConsoleText.resx";

    private static readonly Regex KeyUse = new("""L\["([^"]+)"[\],]""", RegexOptions.CultureInvariant);



    [Fact]
    public void Every_key_the_console_uses_is_defined()
    {
        Assert.Empty(UsedKeys().Except(DefinedKeys(), StringComparer.Ordinal));
    }



    [Fact]
    public void Every_defined_key_is_used()
    {
        Assert.Empty(DefinedKeys().Except(UsedKeys(), StringComparer.Ordinal));
    }



    [Fact]
    public void The_localizer_reads_the_embedded_resource()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = Cultures.ResourcesPath);
        using var provider = services.BuildServiceProvider();
        var text = provider.GetRequiredService<IStringLocalizer<ConsoleText>>();

        Assert.Equal("Wolfgang.Wms", text["product.name"].Value);
        Assert.False(text["product.name"].ResourceNotFound);
        Assert.Equal("Configure", text[Workspaces.Configure.TitleKey].Value);
        Assert.Equal("Scan TOTE-1 is not used on this screen.", text["layout.scan_unused", "TOTE-1"].Value);
        Assert.True(text["no.such.key"].ResourceNotFound);
    }



    private static List<string> DefinedKeys()
    {
        return XDocument
            .Load(Path.Combine(RepositoryFiles.Root, ResxPath))
            .Root!
            .Elements("data")
            .Select(e => (string)e.Attribute("name")!)
            .ToList();
    }



    private static HashSet<string> UsedKeys()
    {
        var inComponents = Directory
            .EnumerateFiles(Path.Combine(RepositoryFiles.Root, "src"), "*.razor", SearchOption.AllDirectories)
            .Where(p => p.Replace('\\', '/').Contains("/src/Wolfgang.Wms.Web", StringComparison.Ordinal)
                        && !p.Replace('\\', '/').Contains("/obj/", StringComparison.Ordinal))
            .SelectMany(p => KeyUse.Matches(File.ReadAllText(p)).Select(m => m.Groups[1].Value));
        var workspaces = Workspaces.All.SelectMany(w => new[] { w.TitleKey, w.DescriptionKey });

        return inComponents.Concat(workspaces).ToHashSet(StringComparer.Ordinal);
    }
}
