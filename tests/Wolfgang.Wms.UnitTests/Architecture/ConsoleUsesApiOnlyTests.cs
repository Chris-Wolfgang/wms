// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Xml.Linq;

namespace Wolfgang.Wms.UnitTests.Architecture;

/// <summary>
/// E82.1: the vendor's console is held to the same API as a customer's tool. The console's projects, named
/// explicitly in <see cref="ConsoleProjects"/> (the host, <c>Web.Shared</c> and the five workspaces, E82.4), may
/// reference only Domain, the API client and each other, never Core, Infrastructure or a data-access package, so
/// nothing they do can be a capability the API lacks. The list is explicit so a new project that merely shares the
/// <c>Wolfgang.Wms.Web</c> prefix is neither treated as a console project nor accepted as a console reference.
/// </summary>
public sealed class ConsoleUsesApiOnlyTests
{
    private static readonly string[] ConsoleProjects =
    [
        "Wolfgang.Wms.Web",
        "Wolfgang.Wms.Web.Configure",
        "Wolfgang.Wms.Web.Insights",
        "Wolfgang.Wms.Web.Report",
        "Wolfgang.Wms.Web.Resolve",
        "Wolfgang.Wms.Web.Shared",
        "Wolfgang.Wms.Web.Supervise",
    ];

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
            .Where(p => ConsoleProjects.Contains(Path.GetFileNameWithoutExtension(p), StringComparer.Ordinal))
            .ToList();

        var offending = consoleProjects
            .SelectMany(p => ConsoleViolationsIn(RepositoryFiles.LoadProject(p)).Select(v => $"{p}: {v}"))
            .ToList();

        Assert.Equal(ConsoleProjects, consoleProjects.Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal));
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
            "<ProjectReference Include=\"..\\Wolfgang.Wms.Web.Shared\\Wolfgang.Wms.Web.Shared.csproj\" />" +
            "<ProjectReference Include=\"..\\Wolfgang.Wms.WebHooks\\Wolfgang.Wms.WebHooks.csproj\" />" +
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
                "project Wolfgang.Wms.WebHooks",
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
            .Where(name => !AllowedProjectReferences.Contains(name, StringComparer.Ordinal) && !ConsoleProjects.Contains(name, StringComparer.Ordinal))
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
