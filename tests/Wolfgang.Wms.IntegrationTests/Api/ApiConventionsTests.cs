// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E82.3 on a throwaway host wired like the real one: problem details carry the code, unhandled exceptions
/// become problem details, responses compress on request, compressed requests are read, and an opted-out
/// endpoint stays uncompressed over TLS.
/// </summary>
public sealed class ApiConventionsTests : IAsyncLifetime
{
    private static readonly ErrorCode ToteMissing = new("picking.tote_missing", StatusCodes.Status404NotFound, "Tote {0} is not on the line.", "tote-missing", ErrorSeverity.Warning);
    private WebApplication? _app;
    private HttpClient? _client;



    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddWmsApiVersioning();
        builder.Services.AddWmsProblemDetails();
        builder.Services.AddWmsCompression();
        _app = builder.Build();

        _app.UseWmsProblemDetails();
        _app.UseWmsCompression();
        var api = _app.MapWmsApi();
        api.MapGet("/totes/{id}", (string id) => ApiProblems.Problem(ToteMissing, arguments: id));
        api.MapGet("/boom", string () => throw new InvalidOperationException("boom"));
        api.MapGet("/text", () => new string('x', 4096));
        api.MapGet("/token", () => new string('t', 4096)).DisableResponseCompression();
        api.MapPost("/echo", async (HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body);
            return await reader.ReadToEndAsync();
        });

        await _app.StartAsync();
        _client = _app.GetTestClient();
        _client.BaseAddress = new Uri("https://localhost/");
    }



    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }



    [Fact]
    public async Task An_error_is_problem_details_with_the_code_severity_and_trace_id()
    {
        using var response = await _client!.GetAsync(new Uri("/api/v0/totes/T-17", UriKind.Relative));
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Tote T-17 is not on the line.", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("picking.tote_missing", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("warning", problem.RootElement.GetProperty("severity").GetString());
        Assert.Equal(ApiProblems.DocsBase + "#tote-missing", problem.RootElement.GetProperty("type").GetString());
        Assert.False(string.IsNullOrEmpty(problem.RootElement.GetProperty("traceId").GetString()));
    }



    [Fact]
    public async Task An_unhandled_exception_is_a_500_problem_without_the_exception_text()
    {
        using var response = await _client!.GetAsync(new Uri("/api/v0/boom", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("boom", body, StringComparison.Ordinal);
    }



    [Theory]
    [InlineData("br")]
    [InlineData("gzip")]
    public async Task A_text_response_is_compressed_with_the_requested_encoding(string encoding)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v0/text", UriKind.Relative));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));

        using var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([encoding], response.Content.Headers.ContentEncoding);
    }



    [Fact]
    public async Task An_opted_out_endpoint_is_not_compressed_over_tls()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v0/token", UriKind.Relative));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

        using var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Content.Headers.ContentEncoding);
    }



    [Fact]
    public async Task A_gzip_compressed_request_body_is_decompressed()
    {
        using var compressed = new MemoryStream();
        await using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            await gzip.WriteAsync("hello from a device"u8.ToArray());
        }

        using var content = new ByteArrayContent(compressed.ToArray());
        content.Headers.ContentEncoding.Add("gzip");
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var response = await _client!.PostAsync(new Uri("/api/v0/echo", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("hello from a device", await response.Content.ReadAsStringAsync());
    }
}
