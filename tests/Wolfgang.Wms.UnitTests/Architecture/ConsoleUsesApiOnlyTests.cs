// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Xml.Linq;

namespace Wolfgang.Wms.UnitTests.Architecture;

/// <summary>
/// E82.1: the vendor's console is held to the same API as a customer's tool. <c>Wolfgang.Wms.Web</c> and the
/// workspace projects (<c>Wolfgang.Wms.Web.*</c>, E82.4) may reference only Domain, the API client and the
/// console's own projects, never Core, Infrastructure or a data-access package, so nothing they do can be a
/// capability the API lacks.
/// </summary>
public sealed class ConsoleUsesApiOnlyTests
{
    private const string ConsoleProject = "src/Wolfgang.Wms.Web/Wolfgang.Wms.Web.csproj";

    private static readonly string[] AllowedProjectReferences = ["Wolfgang.Wms.Domain", "Wolfgang.Wms.Client"];

    private static readonly string[] DeniedPackageFamilies =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Data.SqlClient",
        "Npgsql",
        "Dapper",
        "Wolfgang.DbContextBuilder",
    ];



    [Fact]
    public void Console_and_workspace_projects_reference_only_Domain_the_API_client_and_console_projects()
    {
        var consoleProjects = RepositoryFiles.ProjectPaths()
            .Where(p => p.StartsWith("src/Wolfgang.Wms.Web", StringComparison.Ordinal))
            .ToList();

        var offending = consoleProjects
            .SelectMany(p => ConsoleViolationsIn(RepositoryFiles.LoadProject(p)).Select(v => $"{p}: {v}"))
            .ToList();

        Assert.Contains(ConsoleProject, consoleProjects);
        Assert.True(consoleProjects.Count >= 7, "expected the host, the shared library and five workspaces");
        Assert.Empty(offending);
    }



    [Fact]
    public void Console_check_flags_Core_Infrastructure_and_data_packages()
    {
        var project = XDocument.Parse
        (
            "<Project><ItemGroup>" +
            "<ProjectReference Include=\"..\\Wolfgang.Wms.Domain\\Wolfgang.Wms.Domain.csproj\" />" +
            "<ProjectReference Include=\"..\\Wolfgang.Wms.Core\\Wolfgang.Wms.Core.csproj\" />" +
            "<ProjectReference Include=\"..\\Wolfgang.Wms.Infrastructure\\Wolfgang.Wms.Infrastructure.csproj\" />" +
            "<PackageReference Include=\"Microsoft.EntityFrameworkCore.SqlServer\" Version=\"10.0.0\" />" +
            "<PackageReference Include=\"Microsoft.AspNetCore.Components.QuickGrid\" Version=\"10.0.0\" />" +
            "</ItemGroup></Project>"
        );

        var found = ConsoleViolationsIn(project);

        Assert.Equal
        (
            [
                "package Microsoft.EntityFrameworkCore.SqlServer",
                "project Wolfgang.Wms.Core",
                "project Wolfgang.Wms.Infrastructure",
            ],
            found
        );
    }



    /// <summary>
    /// "project X" for every project reference outside the allow list and "package X" for every package in a
    /// denied family, sorted.
    /// </summary>
    private static List<string> ConsoleViolationsIn(XDocument project)
    {
        var projects = project
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "ProjectReference", StringComparison.Ordinal))
            .Select(e => Path.GetFileNameWithoutExtension(((string?)e.Attribute("Include") ?? string.Empty).Replace('\\', '/')))
            .Where(name => !AllowedProjectReferences.Contains(name, StringComparer.Ordinal) && !name.StartsWith("Wolfgang.Wms.Web", StringComparison.Ordinal))
            .Select(name => $"project {name}");

        var packages = project
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "PackageReference", StringComparison.Ordinal))
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Where(id => DeniedPackageFamilies.Any(family =>
                string.Equals(id, family, StringComparison.OrdinalIgnoreCase)
                || id.StartsWith(family + ".", StringComparison.OrdinalIgnoreCase)))
            .Select(id => $"package {id}");

        return projects
            .Concat(packages)
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
