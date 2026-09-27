// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Xml.Linq;

namespace Wolfgang.Wms.UnitTests.Architecture;

/// <summary>
/// E1.10 (ADR 0001): a module project references only Core and Domain, never another module. A module is any
/// <c>src/Wolfgang.Wms.&lt;Name&gt;</c> project that references <c>Wolfgang.Wms.Core</c> and is not one of the
/// hosts or the core projects.
/// </summary>
public sealed class ModuleDependencyTests
{
    private static readonly string[] NonModuleProjects =
    [
        "Wolfgang.Wms.Domain",
        "Wolfgang.Wms.Core",
        "Wolfgang.Wms.Infrastructure",
        "Wolfgang.Wms.Api",
        "Wolfgang.Wms.Worker",
        "Wolfgang.Wms.Web",
        "Wolfgang.Wms.Android",
        "Wolfgang.Wms.Simulator",
    ];

    private static readonly string[] AllowedForModules = ["Wolfgang.Wms.Core", "Wolfgang.Wms.Domain"];



    [Fact]
    public void Modules_reference_only_Core_and_Domain()
    {
        var projects = RepositoryFiles.ProjectPaths()
            .Where(p => p.StartsWith("src/", StringComparison.Ordinal))
            .Select(p => (Path: p, Project: RepositoryFiles.LoadProject(p)));

        Assert.Empty(OffendingModuleReferences(projects));
    }



    [Fact]
    public void Module_reference_check_flags_a_module_that_references_another_module()
    {
        var project = XDocument.Parse
        (
            "<Project><ItemGroup>" +
            "<ProjectReference Include=\"..\\Wolfgang.Wms.Core\\Wolfgang.Wms.Core.csproj\" />" +
            "<ProjectReference Include=\"..\\Wolfgang.Wms.Domain\\Wolfgang.Wms.Domain.csproj\" />" +
            "<ProjectReference Include=\"..\\Wolfgang.Wms.Inventory\\Wolfgang.Wms.Inventory.csproj\" />" +
            "</ItemGroup></Project>"
        );

        var api = RepositoryFiles.LoadProject("src/Wolfgang.Wms.Api/Wolfgang.Wms.Api.csproj");

        var found = OffendingModuleReferences(
        [
            ("src/Wolfgang.Wms.Picking/Wolfgang.Wms.Picking.csproj", project),
            ("src/Wolfgang.Wms.Api/Wolfgang.Wms.Api.csproj", api),
        ]);

        Assert.Equal(["src/Wolfgang.Wms.Picking/Wolfgang.Wms.Picking.csproj -> Wolfgang.Wms.Inventory"], found);
    }



    [Fact]
    public void Hosts_and_core_projects_are_not_modules()
    {
        var api = RepositoryFiles.LoadProject("src/Wolfgang.Wms.Api/Wolfgang.Wms.Api.csproj");

        Assert.False(IsModule("src/Wolfgang.Wms.Api/Wolfgang.Wms.Api.csproj", api));
    }



    /// <summary>
    /// "path -> referenced project" for every module project that references something other than Core or Domain.
    /// </summary>
    private static List<string> OffendingModuleReferences(IEnumerable<(string Path, XDocument Project)> projects)
    {
        return projects
            .Where(x => IsModule(x.Path, x.Project))
            .SelectMany(x => ForbiddenModuleReferencesIn(x.Project).Select(r => $"{x.Path} -> {r}"))
            .ToList();
    }



    private static bool IsModule(string relativePath, XDocument project)
    {
        var name = Path.GetFileNameWithoutExtension(relativePath);
        return !NonModuleProjects.Contains(name, StringComparer.Ordinal)
               && ProjectReferenceNames(project).Contains("Wolfgang.Wms.Core", StringComparer.Ordinal);
    }



    private static List<string> ForbiddenModuleReferencesIn(XDocument project)
    {
        return ProjectReferenceNames(project)
            .Where(r => !AllowedForModules.Contains(r, StringComparer.Ordinal))
            .ToList();
    }



    private static List<string> ProjectReferenceNames(XDocument project)
    {
        return project
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "ProjectReference", StringComparison.Ordinal))
            .Select(e => Path.GetFileNameWithoutExtension(((string?)e.Attribute("Include") ?? string.Empty).Replace('\\', '/')))
            .ToList();
    }
}
