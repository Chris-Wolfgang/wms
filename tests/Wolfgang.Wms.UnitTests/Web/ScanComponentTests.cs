// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Domain.Localization;
using Wolfgang.Wms.Web.Shared;
using Wolfgang.Wms.Web.Shared.Components;

namespace Wolfgang.Wms.UnitTests.Web;

/// <summary>
/// Renders the console's scan components with bUnit, so the interactive paths (keystrokes, focus, the layout
/// routing a scan to the screen that listens) are exercised, not only the server-prerendered markup.
/// </summary>
public sealed class ScanComponentTests : IDisposable
{
    private readonly BunitContext _context = new();



    public ScanComponentTests()
    {
        // The real console text (ConsoleText.resx), registered the way the host does.
        _context.Services.AddLocalization(options => options.ResourcesPath = Cultures.ResourcesPath);
    }



    [Fact]
    public async Task ScanListener_raises_the_scan_on_Enter_and_keeps_focus()
    {
        var received = new List<string>();
        var listener = _context.Render<ScanListener>(p => p.Add(x => x.OnScan, scan => received.Add(scan)));
        var field = listener.Find("input.scan-listener");

        _context.JSInterop.VerifyFocusAsyncInvoke(1);
        field.Input(new ChangeEventArgs { Value = "  TOTE-0017 " });
        await field.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["TOTE-0017"], received);
        Assert.Equal(string.Empty, listener.Find("input.scan-listener").GetAttribute("value"));
        _context.JSInterop.VerifyFocusAsyncInvoke(2);
    }



    [Fact]
    public async Task ScanListener_ignores_other_keys_and_blank_scans()
    {
        var received = new List<string>();
        var listener = _context.Render<ScanListener>(p => p.Add(x => x.OnScan, scan => received.Add(scan)));
        var field = listener.Find("input.scan-listener");

        field.Input(new ChangeEventArgs { Value = "TOTE" });
        await field.KeyDownAsync(new KeyboardEventArgs { Key = "a" });
        field.Input(new ChangeEventArgs { Value = "   " });
        await field.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Empty(received);
        _context.JSInterop.VerifyFocusAsyncInvoke(1);
    }



    [Fact]
    public async Task ScanListener_FocusAsync_moves_focus_to_the_field_and_the_label_is_settable()
    {
        var listener = _context.Render<ScanListener>(p => p.Add(x => x.Label, "Scan a tote"));

        await listener.InvokeAsync(() => listener.Instance.FocusAsync().AsTask());

        Assert.Equal("Scan a tote", listener.Find("input.scan-listener").GetAttribute("aria-label"));
        _context.JSInterop.VerifyFocusAsyncInvoke(2);
    }



    [Fact]
    public async Task WorkspaceLayout_sends_a_scan_to_the_screen_that_listens()
    {
        _context.Services.AddSingleton<IWorkspaceAccess>(new FixedAccess(WorkspaceAccessResult.Allowed));
        var layout = _context.Render<TestLayout>(p => p.Add(x => x.Body, Screen<ListeningScreen>()));

        await Scan(layout, "TOTE-0017");

        Assert.Equal(["TOTE-0017"], layout.FindComponent<ListeningScreen>().Instance.Received);
        Assert.Empty(layout.FindAll(".scan-feedback"));
    }



    [Fact]
    public async Task WorkspaceLayout_reports_a_scan_no_screen_takes_and_clears_it_once_one_is_taken()
    {
        _context.Services.AddSingleton<IWorkspaceAccess>(new FixedAccess(WorkspaceAccessResult.Allowed));
        var layout = _context.Render<TestLayout>(p => p.Add(x => x.Body, Screen<IdleScreen>()));

        await Scan(layout, "TOTE-0017");

        Assert.Contains("TOTE-0017", layout.Find(".scan-feedback").TextContent, StringComparison.Ordinal);
        Assert.Contains("is not used on this screen", layout.Find(".scan-feedback").TextContent, StringComparison.Ordinal);
    }



    [Fact]
    public void WorkspaceLayout_renders_no_scan_field_when_the_workspace_is_denied()
    {
        _context.Services.AddSingleton<IWorkspaceAccess>(new FixedAccess(WorkspaceAccessResult.NotPermitted));

        var layout = _context.Render<TestLayout>(p => p.Add(x => x.Body, Screen<ListeningScreen>()));

        Assert.Empty(layout.FindAll("input.scan-listener"));
        Assert.Empty(layout.FindComponents<ListeningScreen>());
    }



    [Fact]
    public void WorkspaceLayout_names_the_missing_license_feature_and_renders_no_scan_field()
    {
        _context.Services.AddSingleton<IWorkspaceAccess>(new FixedAccess(WorkspaceAccessResult.NotLicensed));

        var layout = _context.Render<TestLayout>(p => p.Add(x => x.Body, Screen<ListeningScreen>()));

        Assert.Contains("Configure is not licensed", layout.Find("h1").TextContent, StringComparison.Ordinal);
        Assert.Contains(Workspaces.Configure.LicenseFeature.Name, layout.Find(".workspace-denied").TextContent, StringComparison.Ordinal);
        Assert.Empty(layout.FindAll("input.scan-listener"));
    }



    public void Dispose()
    {
        _context.Dispose();
    }



    private static RenderFragment Screen<TScreen>()
        where TScreen : IComponent
    {
        return builder =>
        {
            builder.OpenComponent<TScreen>(0);
            builder.CloseComponent();
        };
    }



    private static async Task Scan(IRenderedComponent<TestLayout> layout, string text)
    {
        var field = layout.Find("input.scan-listener");
        field.Input(new ChangeEventArgs { Value = text });
        await layout.Find("input.scan-listener").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
    }



    private sealed class TestLayout : WorkspaceLayout
    {
        protected override Workspace Workspace => Workspaces.Configure;
    }



    private sealed class FixedAccess(WorkspaceAccessResult result) : IWorkspaceAccess
    {
        public Task<WorkspaceAccessResult> CheckAsync(Workspace workspace, CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }



    private sealed class ListeningScreen : ComponentBase, IDisposable
    {
        private IDisposable? _registration;

        [CascadingParameter]
        public ScanDispatcher? Scans { get; set; }

        public List<string> Received { get; } = [];

        public void Dispose()
        {
            _registration?.Dispose();
        }

        protected override void OnInitialized()
        {
            _registration = Scans!.Listen(scan =>
            {
                Received.Add(scan);
                return Task.CompletedTask;
            });
        }
    }



    private sealed class IdleScreen : ComponentBase
    {
    }
}
