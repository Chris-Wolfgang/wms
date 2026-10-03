// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.CodeDom.Compiler;
using System.Reflection;
using Microsoft.Kiota.Abstractions;
using Wolfgang.Wms.Client;
using Wolfgang.Wms.Client.Generated;
using Wolfgang.Wms.UnitTests.Architecture;

namespace Wolfgang.Wms.UnitTests.Client;

/// <summary>
/// E82.8 (ADR 0004): the client is generated, references only the Kiota runtime, and is built over an
/// <see cref="HttpClient"/> whose base address is the API root.
/// </summary>
public sealed class ClientIsolationTests
{
    private const string ClientProject = "src/Wolfgang.Wms.Client/Wolfgang.Wms.Client.csproj";



    [Fact]
    public void Client_project_references_no_other_project_and_only_the_Kiota_runtime()
    {
        var project = RepositoryFiles.LoadProject(ClientProject);

        var projectReferences = project.Descendants().Where(e => string.Equals(e.Name.LocalName, "ProjectReference", StringComparison.Ordinal)).ToList();
        var packages = project.Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "PackageReference", StringComparison.Ordinal))
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .ToList();

        Assert.Empty(projectReferences);
        Assert.All(packages, id => Assert.StartsWith("Microsoft.Kiota.", id, StringComparison.Ordinal));
    }



    [Fact]
    public void Generated_client_is_marked_as_generated_by_Kiota()
    {
        var attribute = typeof(WmsClient).GetCustomAttribute<GeneratedCodeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Kiota", attribute.Tool);
    }



    [Fact]
    public void Client_assembly_depends_only_on_the_runtime_and_Kiota()
    {
        var references = typeof(WmsClient).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal) && !string.Equals(n, "netstandard", StringComparison.Ordinal))
            .ToList();

        Assert.All(references, n => Assert.StartsWith("Microsoft.Kiota.", n, StringComparison.Ordinal));
    }



    [Fact]
    public void Create_builds_a_client_over_the_HttpClient_base_address()
    {
        using var http = new HttpClient { BaseAddress = new Uri("https://wms.example/") };

        var client = WmsApiClient.Create(http);

        // RequestAdapter is protected on Kiota's BaseRequestBuilder; read it reflectively to check the base URL.
        var adapter = (IRequestAdapter?)typeof(BaseRequestBuilder)
            .GetProperty("RequestAdapter", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client);

        Assert.NotNull(adapter);
        Assert.Equal("https://wms.example", adapter.BaseUrl);
    }



    [Fact]
    public void Create_when_the_HttpClient_is_null_or_has_no_base_address_throws()
    {
        using var http = new HttpClient();

        Assert.Throws<ArgumentNullException>(() => WmsApiClient.Create(null!));
        Assert.Throws<ArgumentException>(() => WmsApiClient.Create(http));
    }
}
