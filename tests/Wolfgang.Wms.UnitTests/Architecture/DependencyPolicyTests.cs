// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Xml.Linq;

namespace Wolfgang.Wms.UnitTests.Architecture;

/// <summary>
/// E1.8: third-party dependencies are minimised and the deny list is enforced by the build, not by memory.
/// </summary>
public sealed class DependencyPolicyTests
{
    /// <summary>
    /// Packages that never enter this repository, with the reason from the dependency policy.
    /// </summary>
    private static readonly Dictionary<string, string> DeniedPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MediatR"] = "handlers are plain classes wired explicitly; no in-process message bus",
        ["AutoMapper"] = "conversions are hand-written ToXxx()/FromXxx() extension methods",
        ["Moq"] = "NSubstitute (BSD-3) is the mocking framework; hand-written fakes for Domain interfaces",
        ["FluentAssertions"] = "xUnit built-in asserts only; no assertion libraries",
        ["Hangfire"] = "worker jobs are hosted services with table-backed scheduling, no job queue",
        ["MassTransit"] = "no message bus; the outbox carries side effects",
    };



    [Fact]
    public void No_project_references_a_denied_package()
    {
        var offending = RepositoryFiles.ProjectPaths()
            .SelectMany(path => DeniedReferencesIn(RepositoryFiles.LoadProject(path)).Select(id => $"{path}: {id}"))
            .ToList();

        Assert.Empty(offending);
    }



    [Fact]
    public void Denied_package_check_matches_by_id_and_by_family_prefix()
    {
        var project = XDocument.Parse
        (
            "<Project><ItemGroup>" +
            "<PackageReference Include=\"mediatr\" Version=\"12.0.0\" />" +
            "<PackageReference Include=\"Moq.AutoMock\" Version=\"3.0.0\" />" +
            "<PackageReference Include=\"xunit\" Version=\"2.9.3\" />" +
            "</ItemGroup></Project>"
        );

        var found = DeniedReferencesIn(project);

        Assert.Equal(["mediatr", "Moq.AutoMock"], found);
    }



    [Fact]
    public void Every_denied_package_carries_a_reason()
    {
        Assert.All(DeniedPackages, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value)));
    }



    /// <summary>
    /// Package ids in the project that are on the deny list, matched exactly or as a family
    /// (<c>Moq.AutoMock</c> is still Moq).
    /// </summary>
    private static List<string> DeniedReferencesIn(XDocument project)
    {
        return project
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "PackageReference", StringComparison.Ordinal))
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Where(id => DeniedPackages.Keys.Any(denied =>
                string.Equals(id, denied, StringComparison.OrdinalIgnoreCase)
                || id.StartsWith(denied + ".", StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
