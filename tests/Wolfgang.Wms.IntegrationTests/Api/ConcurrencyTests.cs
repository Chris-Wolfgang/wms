// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E5.2 on a throwaway host wired like the real one: an update without <c>If-Match</c> is 428, with a stale
/// tag 412, with the current tag it runs; a stale save (<see cref="DbUpdateConcurrencyException"/>) is the
/// same 412 problem through the exception handler <c>AddWmsDatabase</c> registers.
/// </summary>
public sealed class ConcurrencyTests : IAsyncLifetime
{
    private static readonly EntityTag Current = EntityTag.FromRowVersion(0x2A);
    private WebApplication? _app;
    private HttpClient? _client;



    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Wms:Database:Provider"] = "None" });
        builder.Services.AddWmsApiVersioning();
        builder.Services.AddWmsProblemDetails();
        builder.Services.AddWmsDatabase(builder.Configuration);
        _app = builder.Build();

        _app.UseWmsProblemDetails();
        var api = _app.MapWmsApi();
        api.MapPut("/things/{id}", (HttpRequest request, string id) => Preconditions.RequireIfMatch(request, Current) ?? Results.Ok(id));
        api.MapPut("/stale", string () => throw new DbUpdateConcurrencyException("row_version changed"));

        await _app.StartAsync();
        _client = _app.GetTestClient();
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
    public async Task An_update_without_If_Match_is_428()
    {
        using var response = await Put("/api/v0/things/1", ifMatch: null);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
        Assert.Equal("concurrency.precondition_required", problem.RootElement.GetProperty("code").GetString());
    }



    [Fact]
    public async Task An_update_with_a_stale_If_Match_is_412()
    {
        using var response = await Put("/api/v0/things/1", ifMatch: EntityTag.FromRowVersion(0x29).Value);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("concurrency.precondition_failed", problem.RootElement.GetProperty("code").GetString());
    }



    [Fact]
    public async Task An_update_with_the_current_If_Match_runs()
    {
        using var response = await Put("/api/v0/things/1", ifMatch: Current.Value);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }



    [Fact]
    public async Task A_stale_save_is_the_same_412_problem()
    {
        using var response = await Put("/api/v0/stale", ifMatch: null);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("concurrency.precondition_failed", problem.RootElement.GetProperty("code").GetString());
    }



    [Fact]
    public async Task Other_exceptions_are_not_turned_into_412()
    {
        var handler = new ConcurrencyExceptionHandler();
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("other"), CancellationToken.None);

        Assert.False(handled);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await handler.TryHandleAsync(null!, new InvalidOperationException(), CancellationToken.None));
    }



    private async Task<HttpResponseMessage> Put(string path, string? ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(path, UriKind.Relative)) { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await _client!.SendAsync(request);
    }
}
