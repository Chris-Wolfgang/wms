// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wolfgang.Wms.Client;
using Wolfgang.Wms.Domain.Localization;
using Wolfgang.Wms.Web.Shared;
using Wolfgang.Wms.Web.Shared.Components;

namespace Wolfgang.Wms.UnitTests.Web;

/// <summary>
/// E9.3 in the console: the workspace chrome shows the break-glass banner while a window is open and nothing
/// otherwise; the API-backed source reads <c>GET /auth/local/status</c> and yields a notice only for an open
/// window, swallowing an unreachable or failing API; the placeholder never yields one.
/// </summary>
public sealed class LocalLoginNoticeTests : IDisposable
{
    private static readonly DateTimeOffset Until = new(2026, 10, 7, 12, 30, 0, TimeSpan.Zero);

    private readonly BunitContext _context = new();



    public LocalLoginNoticeTests()
    {
        _context.Services.AddLocalization(options => options.ResourcesPath = Cultures.ResourcesPath);
        _context.Services.AddSingleton<IWorkspaceAccess>(new FixedAccess(WorkspaceAccessResult.Allowed));
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
    }



    [Fact]
    public void The_layout_shows_the_banner_while_a_window_is_open()
    {
        _context.Services.AddSingleton<ILocalLoginNotice>(new FixedNotice(new LocalLoginNotice(Until)));

        var layout = _context.Render<TestLayout>();

        var banner = layout.Find(".break-glass-banner");
        Assert.Equal("status", banner.GetAttribute("role"));
        Assert.Contains("Local sign-in is unlocked until", banner.TextContent, StringComparison.Ordinal);
        Assert.Contains("wms-admin lock", banner.TextContent, StringComparison.Ordinal);
    }



    [Fact]
    public void The_layout_shows_no_banner_without_a_window()
    {
        _context.Services.AddSingleton<ILocalLoginNotice>(new NoLocalLoginNotice());

        var layout = _context.Render<TestLayout>();

        Assert.Empty(layout.FindAll(".break-glass-banner"));
        Assert.NotEmpty(layout.FindAll("input.scan-listener"));   // the workspace itself still renders
    }



    [Theory]
    [InlineData("""{ "localLoginOpen": true, "ssoVerified": true, "unlockedUntil": "2026-10-07T12:30:00+00:00", "forcedLocal": false }""", true)]
    [InlineData("""{ "localLoginOpen": true, "ssoVerified": false, "unlockedUntil": null, "forcedLocal": false }""", false)]
    [InlineData("""{ "localLoginOpen": false, "ssoVerified": true, "unlockedUntil": null, "forcedLocal": false }""", false)]
    public async Task The_api_source_yields_a_notice_only_for_an_open_window(string body, bool expectNotice)
    {
        using var http = new HttpClient(new StubHandler(_ => Json(HttpStatusCode.OK, body))) { BaseAddress = new Uri("https://wms.example/") };
        var source = new ApiLocalLoginNotice(WmsApiClient.Create(http), NullLogger<ApiLocalLoginNotice>.Instance);

        var notice = await source.GetAsync(CancellationToken.None);

        Assert.Equal(expectNotice ? new LocalLoginNotice(Until) : null, notice);
    }



    [Fact]
    public async Task The_api_source_yields_nothing_when_the_api_fails_or_is_unreachable()
    {
        using var failing = new HttpClient(new StubHandler(_ => Json(HttpStatusCode.InternalServerError, """{ "code": "boom" }"""))) { BaseAddress = new Uri("https://wms.example/") };
        using var unreachable = new HttpClient(new StubHandler(_ => throw new HttpRequestException("no route"))) { BaseAddress = new Uri("https://wms.example/") };

        Assert.Null(await new ApiLocalLoginNotice(WmsApiClient.Create(failing), NullLogger<ApiLocalLoginNotice>.Instance).GetAsync(CancellationToken.None));
        Assert.Null(await new ApiLocalLoginNotice(WmsApiClient.Create(unreachable), NullLogger<ApiLocalLoginNotice>.Instance).GetAsync(CancellationToken.None));
        Assert.Null(await new NoLocalLoginNotice().GetAsync(CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => new ApiLocalLoginNotice(null!, NullLogger<ApiLocalLoginNotice>.Instance));
        Assert.Throws<ArgumentNullException>(() => new ApiLocalLoginNotice(WmsApiClient.Create(failing), null!));
    }



    public void Dispose()
    {
        _context.Dispose();
    }



    private static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }



    private sealed class TestLayout : WorkspaceLayout
    {
        protected override Workspace Workspace => Workspaces.Supervise;
    }



    private sealed class FixedAccess(WorkspaceAccessResult result) : IWorkspaceAccess
    {
        public Task<WorkspaceAccessResult> CheckAsync(Workspace workspace, CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }



    private sealed class FixedNotice(LocalLoginNotice? notice) : ILocalLoginNotice
    {
        public Task<LocalLoginNotice?> GetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(notice);
        }
    }



    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(respond(request));
        }
    }
}
