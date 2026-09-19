// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E82.2: one OpenAPI document per served version, and the copy committed under <c>docs/api/</c> is the
/// document the host actually serves. Set <c>WMS_UPDATE_OPENAPI=1</c> to rewrite the committed copies.
/// </summary>
public sealed class OpenApiDocumentTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string UpdateVariable = "WMS_UPDATE_OPENAPI";
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private readonly WebApplicationFactory<Program> _factory;



    public OpenApiDocumentTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task Each_served_version_has_an_OpenApi_document_named_after_it()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v0.json", UriKind.Relative));
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("3.", document.RootElement.GetProperty("openapi").GetString(), StringComparison.Ordinal);
        Assert.Equal("Wolfgang.Wms API", document.RootElement.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("0", document.RootElement.GetProperty("info").GetProperty("version").GetString());
    }



    [Fact]
    public async Task Unknown_version_document_is_not_served()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v9.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }



    [Fact]
    public async Task Committed_document_matches_the_served_document()
    {
        using var client = _factory.CreateClient();
        var path = Path.Combine(RepositoryRoot(), "docs", "api", "openapi-v0.json");
        var update = string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal);

        var served = Normalize(await client.GetStringAsync(new Uri("/openapi/v0.json", UriKind.Relative)));
        var committed = await ReadOrUpdateCommittedAsync(path, served, update);

        Assert.Equal(served, committed);
    }



    [Fact]
    public async Task Update_mode_rewrites_the_committed_document_with_LF_line_endings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "wms-openapi-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "openapi-v0.json");
        var served = "{\r\n  \"openapi\": \"3.1.1\"\r\n}";
        try
        {
            var committed = await ReadOrUpdateCommittedAsync(path, served, update: true);

            Assert.Equal(Normalize(served), committed);
            Assert.Equal("{\n  \"openapi\": \"3.1.1\"\n}\n", await File.ReadAllTextAsync(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }



    /// <summary>
    /// The committed document, normalised; with <paramref name="update"/> the served document is written first
    /// (LF line endings, so the committed file is identical on every platform).
    /// </summary>
    private static async Task<string> ReadOrUpdateCommittedAsync(string path, string served, bool update)
    {
        if (update)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, served.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
        }

        Assert.True(File.Exists(path), $"{path} is missing; run the tests once with {UpdateVariable}=1");
        return Normalize(await File.ReadAllTextAsync(path));
    }



    /// <summary>
    /// Re-serialised with stable indentation so formatting and line endings never count as a difference.
    /// </summary>
    private static string Normalize(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, Indented);
    }



    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wolfgang.Wms.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (Wolfgang.Wms.slnx) not found above " + AppContext.BaseDirectory);
    }
}
