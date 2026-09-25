// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Reflection;
using System.Xml.Linq;

namespace Wolfgang.Wms.UnitTests.Architecture;

/// <summary>
/// Pins the E1.1 project-boundary rules so a stray package or project reference fails the build here
/// rather than surfacing as an architectural regression later.
/// </summary>
public sealed class ProjectLayoutTests
{
    private static readonly string[] ForbiddenInDomain =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Microsoft.Maui",
    ];



    [Fact]
    public void Domain_assembly_references_neither_EF_Core_nor_ASP_NET_nor_MAUI()
    {
        var domain = Assembly.Load("Wolfgang.Wms.Domain");

        var offending = domain
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => ForbiddenInDomain.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(offending);
    }



    [Fact]
    public void Domain_project_declares_no_forbidden_package_or_framework_references()
    {
        var project = LoadProject("src/Wolfgang.Wms.Domain/Wolfgang.Wms.Domain.csproj");

        Assert.Empty(ForbiddenReferencesIn(project));
    }



    [Fact]
    public void Forbidden_reference_check_reports_an_EF_Core_package_and_an_ASP_NET_framework_reference()
    {
        var project = XDocument.Parse
        (
            "<Project><ItemGroup>" +
            "<PackageReference Include=\"Microsoft.EntityFrameworkCore\" Version=\"10.0.0\" />" +
            "<FrameworkReference Include=\"Microsoft.AspNetCore.App\" />" +
            "<PackageReference Include=\"System.Text.Json\" Version=\"10.0.0\" />" +
            "</ItemGroup></Project>"
        );

        var offending = ForbiddenReferencesIn(project);

        Assert.Equal(["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore.App"], offending);
    }



    private static List<string> ForbiddenReferencesIn(XDocument project)
    {
        return project
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "PackageReference", StringComparison.Ordinal)
                     || string.Equals(e.Name.LocalName, "FrameworkReference", StringComparison.Ordinal))
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Where(r => ForbiddenInDomain.Any(prefix => r.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();
    }



    [Theory]
    [InlineData("src/Wolfgang.Wms.Android/Wolfgang.Wms.Android.csproj")]
    [InlineData("src/Wolfgang.Wms.Api/Wolfgang.Wms.Api.csproj")]
    [InlineData("src/Wolfgang.Wms.Worker/Wolfgang.Wms.Worker.csproj")]
    [InlineData("src/Wolfgang.Wms.Simulator/Wolfgang.Wms.Simulator.csproj")]
    public void Host_projects_reference_Domain(string relativeProjectPath)
    {
        var project = LoadProject(relativeProjectPath);

        var projectReferences = project
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "ProjectReference", StringComparison.Ordinal))
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .ToList();

        Assert.Contains
        (
            projectReferences,
            r => r.Replace('\\', '/').EndsWith("/Wolfgang.Wms.Domain/Wolfgang.Wms.Domain.csproj", StringComparison.Ordinal)
        );
    }



    [Fact]
    public void Repository_root_lookup_fails_outside_the_repository()
    {
        var outside = Path.GetTempPath();

        var exception = Assert.Throws<InvalidOperationException>(() => RepositoryFiles.FindRoot(outside));

        Assert.Contains("Wolfgang.Wms.slnx", exception.Message, StringComparison.Ordinal);
    }



    private static XDocument LoadProject(string relativeProjectPath)
    {
        return RepositoryFiles.LoadProject(relativeProjectPath);
    }
}
