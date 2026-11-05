// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
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
    private const string RefocusModule = "./_content/Wolfgang.Wms.Web.Shared/Components/ScanListener.razor.js";

    private readonly BunitContext _context = new();



    public ScanComponentTests()
    {
        // The real console text (ConsoleText.resx), registered the way the host does.
        _context.Services.AddLocalization(options => options.ResourcesPath = Cultures.ResourcesPath);
        _context.Services.AddLogging();

        // The refocus script is asserted by the tests that set it up; the others only need it to load.
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
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
    public async Task ScanListener_attaches_the_refocus_script_to_the_field_and_detaches_it_when_disposed()
    {
        var module = _context.JSInterop.SetupModule(RefocusModule);
        var refocus = module.SetupModule(invocation => string.Equals(invocation.Identifier, "attach", StringComparison.Ordinal));
        refocus.SetupVoid("detach").SetVoidResult();
        var field = _context.Render<ScanListener>().Find("input.scan-listener").GetAttribute("blazor:elementReference");

        await _context.DisposeComponentsAsync();

        Assert.Equal
        (
            field,
            ((ElementReference)module.VerifyInvoke("attach").Arguments[0]!).Id
        );
        refocus.VerifyInvoke("detach");
    }



    [Fact]
    public async Task ScanListener_dispose_tolerates_a_closed_circuit()
    {
        var module = _context.JSInterop.SetupModule(RefocusModule);
        var refocus = module.SetupModule(invocation => string.Equals(invocation.Identifier, "attach", StringComparison.Ordinal));
        refocus.SetupVoid("detach").SetException(new JSDisconnectedException("The circuit is closed."));
        _context.Render<ScanListener>();

        await _context.DisposeComponentsAsync();

        refocus.VerifyInvoke("detach");
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
    public async Task WorkspaceLayout_reports_a_scan_whose_handler_threw_and_keeps_the_chrome()
    {
        _context.Services.AddSingleton<IWorkspaceAccess>(new FixedAccess(WorkspaceAccessResult.Allowed));
        var layout = _context.Render<TestLayout>(p => p.Add(x => x.Body, Screen<ThrowingScreen>()));

        await Scan(layout, "TOTE-0017");
        var feedback = layout.Find(".scan-feedback");
        var role = feedback.GetAttribute("role");
        var text = feedback.TextContent;
        layout.FindComponent<ThrowingScreen>().Instance.Throws = false;
        await Scan(layout, "TOTE-0018");

        Assert.Equal("alert", role);
        Assert.Equal("Scan TOTE-0017 could not be handled by this screen.", text);
        Assert.NotEmpty(layout.FindAll("header.workspace-header"));   // the chrome survived the handler's failure
        Assert.NotEmpty(layout.FindAll("input.scan-listener"));      // and so did the scanner
        Assert.Empty(layout.FindAll(".scan-feedback"));               // the next scan, handled, clears the message
        Assert.Equal(["TOTE-0018"], layout.FindComponent<ThrowingScreen>().Instance.Received);
    }



    [Fact]
    public async Task WorkspaceLayout_lets_a_cancelled_scan_handler_propagate_instead_of_reporting_it()
    {
        _context.Services.AddSingleton<IWorkspaceAccess>(new FixedAccess(WorkspaceAccessResult.Allowed));
        var layout = _context.Render<TestLayout>(p => p.Add(x => x.Body, Screen<CancellingScreen>()));

        await Scan(layout, "TOTE-0017");

        Assert.True(layout.FindComponent<CancellingScreen>().Instance.Invoked);
        Assert.Empty(layout.FindAll(".scan-feedback"));                  // a cancellation is not a failed handler, so nothing is reported
        Assert.False(_context.Renderer.UnhandledException.IsCompleted);   // and the renderer saw a cancelled task, not an error
        Assert.NotEmpty(layout.FindAll("input.scan-listener"));
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



    /// <summary>
    /// A screen whose scan listener throws until <see cref="Throws"/> is cleared, then records the scan.
    /// </summary>
    private sealed class ThrowingScreen : ComponentBase, IDisposable
    {
        private IDisposable? _registration;

        [CascadingParameter]
        public ScanDispatcher? Scans { get; set; }

        public bool Throws { get; set; } = true;

        public List<string> Received { get; } = [];

        public void Dispose()
        {
            _registration?.Dispose();
        }

        protected override void OnInitialized()
        {
            _registration = Scans!.Listen(scan =>
            {
                if (Throws)
                {
                    throw new InvalidOperationException("The handler failed.");
                }

                Received.Add(scan);
                return Task.CompletedTask;
            });
        }
    }



    /// <summary>
    /// A screen whose scan listener is cancelled: the layout must let that through rather than report it.
    /// </summary>
    private sealed class CancellingScreen : ComponentBase, IDisposable
    {
        private IDisposable? _registration;

        [CascadingParameter]
        public ScanDispatcher? Scans { get; set; }

        public bool Invoked { get; private set; }

        public void Dispose()
        {
            _registration?.Dispose();
        }

        protected override void OnInitialized()
        {
            _registration = Scans!.Listen(_ =>
            {
                Invoked = true;
                throw new OperationCanceledException();
            });
        }
    }
}
